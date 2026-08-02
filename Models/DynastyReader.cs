using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace PinkSlipsTool.Models;

public class BackupEntry
{
    public string Path { get; init; }
    public DateTime Timestamp { get; init; }
    public string FileName => System.IO.Path.GetFileName(Path);
    public string Display => $"{Timestamp:MM/dd/yyyy  h:mm tt}   {FileName}";
}

public class DynastyFile
{
    public byte[] RawBytes { get; set; }
    public byte[] DecompressedPayload { get; set; }
    public List<FranchiseTable> Tables { get; set; } = new();
    public int DeflateStartOffset { get; set; }
    // Offset in RawBytes just past the original zlib adler32 trailer. Everything from here
    // to EOF (the post-stream tail records + padding) is preserved verbatim on save.
    public int PostStreamOffset { get; set; } = -1;
    public string LoadedPath { get; set; }
    public string BackupPath { get; set; }

    public FranchiseTable GetTable(int tableId) =>
        Tables.Find(t => t.Header.TableId == tableId);

    public FranchiseTable GetTableByName(string name) =>
        Tables.Find(t => t.Header.Name == name);

    public void CreateBackup()
    {
        if (LoadedPath == null) return;
        var dir = Path.GetDirectoryName(LoadedPath);
        var name = Path.GetFileNameWithoutExtension(LoadedPath);
        BackupPath = Path.Combine(dir, $"{name}.{DateTime.Now:yyyyMMdd-HHmmss}.bak");
        File.Copy(LoadedPath, BackupPath, overwrite: false);
    }

    public void RestoreBackup()
    {
        RestoreFrom(BackupPath);
    }

    public void RestoreFrom(string backupPath)
    {
        if (backupPath == null || !File.Exists(backupPath)) return;
        // Re-load from backup into current instance
        var restored = Load(backupPath);
        RawBytes = restored.RawBytes;
        DecompressedPayload = restored.DecompressedPayload;
        Tables = restored.Tables;
        DeflateStartOffset = restored.DeflateStartOffset;
        PostStreamOffset = restored.PostStreamOffset;
        BackupPath = backupPath;
    }

    // Backups are created as "{name}.{yyyyMMdd-HHmmss}.bak" next to the loaded file.
    // Enumerate all of them, newest first, so the user can restore to any point in time.
    public List<BackupEntry> FindBackups()
    {
        var list = new List<BackupEntry>();
        if (LoadedPath == null) return list;

        var dir = Path.GetDirectoryName(LoadedPath);
        var name = Path.GetFileNameWithoutExtension(LoadedPath);
        // After a restore, LoadedPath becomes a derived "{name}.{timestamp}" path; strip that
        // suffix so we always match the original file's backups, not backups-of-a-backup.
        var suffix = Regex.Match(name, @"^(.*)\.\d{8}-\d{6}$");
        if (suffix.Success) name = suffix.Groups[1].Value;
        if (dir == null || !Directory.Exists(dir)) return list;

        foreach (var file in Directory.GetFiles(dir, $"{name}.*.bak"))
        {
            var baseName = Path.GetFileNameWithoutExtension(file);
            var stamp = baseName.Length > name.Length + 1 ? baseName[(name.Length + 1)..] : "";
            var ts = DateTime.MinValue;
            if (DateTime.TryParseExact(stamp, "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
                ts = parsed;
            list.Add(new BackupEntry { Path = file, Timestamp = ts });
        }

        list.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
        return list;
    }

    public static DynastyFile Load(string path)
    {
        var data = File.ReadAllBytes(path);
        var file = Parse(data);
        file.LoadedPath = path;
        return file;
    }

    public static DynastyFile Parse(byte[] data)
    {
        if (data.Length < 8 || System.Text.Encoding.ASCII.GetString(data, 0, 8) != "FBCHUNKS")
            throw new InvalidDataException("Not a valid Frostbite save file.");

        // Find zlib header (0x78 0x9C or 0x78 0x01 or 0x78 0xDA)
        var deflateStart = -1;
        for (var i = 8; i < Math.Min(data.Length - 2, 256); i++)
        {
            if (data[i] == 0x78 && (data[i + 1] == 0x9C || data[i + 1] == 0x01 || data[i + 1] == 0xDA))
            {
                deflateStart = i + 2;
                break;
            }
        }
        if (deflateStart < 0)
            throw new InvalidDataException("Could not find zlib header in FBCHUNKS file.");

        using var outputStream = new MemoryStream();
        using var inputStream = new MemoryStream(data, deflateStart, data.Length - deflateStart);
        using var deflate = new DeflateStream(inputStream, CompressionMode.Decompress);
        deflate.CopyTo(outputStream);
        var payload = outputStream.ToArray();

        var tables = FranchiseTableParser.ScanTables(payload);

        return new DynastyFile
        {
            RawBytes = data,
            DecompressedPayload = payload,
            Tables = tables,
            DeflateStartOffset = deflateStart,
            PostStreamOffset = FindPostStreamOffset(data, deflateStart, payload)
        };
    }

    // The zlib stream is terminated by a 4-byte big-endian adler32 of the payload. Locate it
    // (the first occurrence past the deflate data) and return the offset just after it.
    private static int FindPostStreamOffset(byte[] data, int deflateStart, byte[] payload)
    {
        var adler = ComputeAdler32(payload);
        var p0 = (byte)(adler >> 24);
        var p1 = (byte)(adler >> 16);
        var p2 = (byte)(adler >> 8);
        var p3 = (byte)adler;
        for (var i = deflateStart; i + 4 < data.Length; i++)
        {
            if (data[i] == p0 && data[i + 1] == p1 && data[i + 2] == p2 && data[i + 3] == p3)
                return i + 4;
        }
        return -1;
    }

    public static uint ComputeAdler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var x in data)
        {
            a = (a + x) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }

    public void SyncTable(FranchiseTable table)
    {
        Array.Copy(table.Data, 0, DecompressedPayload, table.AbsoluteStart, table.Data.Length);
    }

    public void Save(string path = null)
    {
        var outPath = path ?? LoadedPath;
        if (outPath == null || RawBytes == null || DecompressedPayload == null) return;

        // Recompress the (possibly edited) payload. SmallestSize keeps the output close to the
        // game's own compression level so it fits back inside the original fixed-size allocation.
        byte[] deflateBytes;
        using (var compressedStream = new MemoryStream())
        {
            using (var deflate = new DeflateStream(compressedStream, CompressionLevel.SmallestSize, leaveOpen: true))
                deflate.Write(DecompressedPayload, 0, DecompressedPayload.Length);
            deflateBytes = compressedStream.ToArray();
        }

        // Standard zlib stream: 78 9C header, raw deflate, then 4-byte big-endian adler32
        // trailer. The game's zlib reader validates the trailer, so it must be present.
        var adler = ComputeAdler32(DecompressedPayload);

        var headerEnd = DeflateStartOffset - 2;
        if (headerEnd < 0) headerEnd = 0;

        // Rebuild at the original fixed size: header verbatim, then the new zlib stream, then
        // the original post-stream tail records verbatim, zero-padded to the original length.
        var targetLen = RawBytes.Length;
        var minLen = headerEnd + 2 + deflateBytes.Length + 4;
        if (targetLen < minLen) targetLen = minLen;

        var result = new byte[targetLen];
        Array.Copy(RawBytes, 0, result, 0, headerEnd);
        result[headerEnd] = 0x78;
        result[headerEnd + 1] = 0x9C;
        Array.Copy(deflateBytes, 0, result, headerEnd + 2, deflateBytes.Length);

        var adlerPos = headerEnd + 2 + deflateBytes.Length;
        result[adlerPos] = (byte)(adler >> 24);
        result[adlerPos + 1] = (byte)(adler >> 16);
        result[adlerPos + 2] = (byte)(adler >> 8);
        result[adlerPos + 3] = (byte)adler;

        if (PostStreamOffset > 0 && PostStreamOffset < RawBytes.Length)
        {
            var tailLen = RawBytes.Length - PostStreamOffset;
            var tailDest = adlerPos + 4;
            if (tailLen <= targetLen - tailDest)
                Array.Copy(RawBytes, PostStreamOffset, result, tailDest, tailLen);
        }

        // FBCHUNKS container: [0x08]=01 00, [0x0A..0x0E)=header_size (LE),
        // [0x0E..0x12)=data_size (LE), [0x12..0x16)=header_size+data_size (LE).
        // The data block starts at 0x12 + header_size and is exactly data_size bytes long.
        // When the fixed-size rebuild holds, these match the original values; recompute anyway
        // so a longer recompressed stream (which cannot fit) still yields consistent fields.
        if (result.Length >= 0x12)
        {
            var headerSize = BitConverter.ToUInt32(result, 0x0A);
            var dataStart = 0x12 + (int)headerSize;
            var dataSize = result.Length - dataStart;
            if (dataSize >= 0)
            {
                BitConverter.GetBytes((uint)dataSize).CopyTo(result, 0x0E);
                BitConverter.GetBytes((uint)(result.Length - 0x12)).CopyTo(result, 0x12);
            }
        }

        File.WriteAllBytes(outPath, result);
    }
}
