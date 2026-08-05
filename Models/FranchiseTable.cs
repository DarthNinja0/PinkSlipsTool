using System.Text;

namespace PinkSlipsTool.Models;

public class FranchiseTableHeader
{
    public string Name { get; init; }
    public bool IsArray { get; init; }
    public int TableId { get; init; }
    public int UniqueId { get; init; }
    public int RecordCount { get; init; }
    public int Table1Length { get; init; }
    public int Table2Length { get; init; }
    public int Table3Length { get; init; }
    public int RecordSize { get; init; }
    public int RecordCapacity { get; init; }
    public int NumMembers { get; init; }
    public int NextRecordToUse { get; init; }
    public int TableStoreLength { get; init; }
    public int RecordWords { get; init; }
    public int OffsetStart { get; init; }
    public int Table1Start { get; init; }
    public int Table2Start { get; init; }
    public int Table3Start { get; init; }
}

public class FranchiseTable
{
    public int AbsoluteStart { get; init; }
    public int AbsoluteEnd { get; init; }
    public FranchiseTableHeader Header { get; init; }
    public byte[] Data { get; init; }
    public int[] FieldOffsets { get; set; }
    public int[] FieldBitWidths { get; set; }

    public byte[] GetRecordBytes(int index)
    {
        var count = Header.IsArray ? Header.RecordCount : Header.NextRecordToUse;
        if (index < 0 || index >= count)
            return null;
        var off = Header.Table1Start + index * Header.RecordSize;
        var result = new byte[Header.RecordSize];
        Array.Copy(Data, off, result, 0, Header.RecordSize);
        return result;
    }

    public void WriteRecordBytes(int index, byte[] record)
    {
        var count = Header.IsArray ? Header.RecordCount : Header.NextRecordToUse;
        if (index < 0 || index >= count) return;
        if (record == null || record.Length != Header.RecordSize) return;
        var off = Header.Table1Start + index * Header.RecordSize;
        Array.Copy(record, 0, Data, off, Header.RecordSize);
    }

    // ---- Array (ASTO) table helpers ------------------------------------
    // Array rows are fixed-size lists of 32-bit references. Header.OffsetStart
    // points at the per-row "arraySizes" table (one 32-bit count per row).

    public int ReadArraySize(int index)
    {
        if (!Header.IsArray || index < 0 || index >= Header.RecordCount) return -1;
        return ReadU32BE(Data, Header.OffsetStart + index * 4);
    }

    public void WriteArraySize(int index, int value)
    {
        if (!Header.IsArray || index < 0 || index >= Header.RecordCount) return;
        var off = Header.OffsetStart + index * 4;
        Data[off] = (byte)(value >> 24);
        Data[off + 1] = (byte)(value >> 16);
        Data[off + 2] = (byte)(value >> 8);
        Data[off + 3] = (byte)value;
    }

    public uint ReadArrayRowRef(int row, int slot)
    {
        if (!Header.IsArray) return 0;
        var off = Header.Table1Start + row * Header.RecordSize + slot * 4;
        if (off < 0 || off + 4 > Data.Length) return 0;
        return (uint)((Data[off] << 24) | (Data[off + 1] << 16) | (Data[off + 2] << 8) | Data[off + 3]);
    }

    public void WriteArrayRowRef(int row, int slot, uint value)
    {
        if (!Header.IsArray) return;
        var off = Header.Table1Start + row * Header.RecordSize + slot * 4;
        if (off < 0 || off + 4 > Data.Length) return;
        Data[off] = (byte)(value >> 24);
        Data[off + 1] = (byte)(value >> 16);
        Data[off + 2] = (byte)(value >> 8);
        Data[off + 3] = (byte)value;
    }

    public int[] ReadRawOffsetTable()
    {
        var result = new int[Header.NumMembers];
        for (var i = 0; i < Header.NumMembers; i++)
            result[i] = ReadU32BE(Data, Header.OffsetStart + i * 4);
        return result;
    }

    public int[] ComputeBitWidthsFromOffsets()
    {
        var widths = new int[Header.NumMembers];
        if (Header.NumMembers == 0) return widths;

        var indexed = new (int index, int offset)[Header.NumMembers];
        for (var i = 0; i < Header.NumMembers; i++)
            indexed[i] = (i, FieldOffsets[i]);

        Array.Sort(indexed, (a, b) => a.offset.CompareTo(b.offset));

        for (var i = 0; i < indexed.Length - 1; i++)
            widths[indexed[i].index] = indexed[i + 1].offset - indexed[i].offset;

        var lastIdx = indexed[^1].index;
        widths[lastIdx] = Header.RecordSize * 8 - indexed[^1].offset;

        return widths;
    }

    // Some tables (e.g. Player, Coach) store records in a packed layout that differs from
    // the raw offset table. Repacking reproduces the actual record bit layout and the
    // field widths in that packed layout. Verified against CFB25 saves.
    public (int[] offsets, int[] widths) ComputePackedLayout()
    {
        var numMembers = Header.NumMembers;
        var recordBits = Header.RecordSize * 8;
        var raw = ReadRawOffsetTable();

        // Skip duplicate offsets: the first occurrence (in member order) owns the field.
        var skipped = new bool[numMembers];
        var seen = new HashSet<int>();
        for (var i = 0; i < numMembers; i++)
        {
            if (seen.Add(raw[i])) continue;
            skipped[i] = true;
        }

        // Stable sort by raw offset.
        var order = Enumerable.Range(0, numMembers).OrderBy(i => raw[i]).ToArray();

        // Field length = gap to the next non-skipped offset, capped at 32 bits.
        var lengths = new int[numMembers];
        for (var i = 0; i < order.Length; i++)
        {
            var cur = order[i];
            if (skipped[cur]) continue;
            var j = i + 1;
            while (j < order.Length && skipped[order[j]]) j++;
            lengths[cur] = j < order.Length
                ? raw[order[j]] - raw[cur]
                : recordBits - raw[cur];
            if (lengths[cur] > 32) lengths[cur] = 32;
        }

        // Pack fields into 32-bit words; each word's last field anchors at the raw
        // offset of the word's first field.
        var packed = new int[numMembers];
        var coi = 0;
        for (var word = 0; word < recordBits; word += 32)
        {
            var chunk = new List<int>();
            var offsetLength = word % 32;
            while (true)
            {
                if (coi >= numMembers) break;
                var cur = order[coi];
                if (skipped[cur]) { coi++; continue; }
                offsetLength += lengths[cur];
                chunk.Add(cur);
                coi++;
                if (coi >= numMembers || offsetLength >= 32) break;
            }
            if (chunk.Count == 0) continue;
            var last = chunk.Count - 1;
            var anchor = raw[chunk[0]];
            packed[chunk[last]] = anchor;
            for (var k = last - 1; k >= 0; k--)
                packed[chunk[k]] = packed[chunk[k + 1]] + lengths[chunk[k + 1]];
        }

        return (packed, lengths);
    }

    // Resolve a string-pool pointer (field stores an offset from Table2Start).
    public string ResolvePoolString(int ptr)
    {
        var abs = Header.Table2Start + ptr;
        if (ptr < 0 || abs < 0 || abs >= Data.Length) return "";
        return RecordCodec.ReadCStringAt(Data, abs);
    }

    private static int ReadU32BE(byte[] buf, int offset) =>
        (buf[offset] << 24) | (buf[offset + 1] << 16) | (buf[offset + 2] << 8) | buf[offset + 3];
}

public static class FranchiseTableParser
{
    private static readonly byte[] MagicSPBF = "SPBF"u8.ToArray();
    private const int TableStartOffset = 0x94;
    private const int HeaderStart = 0x80;

    // Tables whose records are stored packed (repacked layout) rather than at their
    // raw offset-table positions. Verified against the CFB25 (C27) save schema:
    // Player=4248, Coach=4176. Team (6311) and others use raw offsets directly.
    private static readonly HashSet<int> PackedTableIds = new() { 4248, 4176 };

    public static List<FranchiseTable> ScanTables(byte[] payload)
    {
        var starts = FindTableStarts(payload);
        starts.Sort();
        var tables = new List<FranchiseTable>();
        for (var i = 0; i < starts.Count; i++)
        {
            var start = starts[i];
            var end = i + 1 < starts.Count ? starts[i + 1] : payload.Length;
            var len = Math.Min(end - start, payload.Length - start);
            if (len < 200) continue;
            var tableBuf = new byte[len];
            Array.Copy(payload, start, tableBuf, 0, len);
            try
            {
                var header = ParseTableHeader(tableBuf);
                var table = new FranchiseTable
                {
                    AbsoluteStart = start,
                    AbsoluteEnd = end,
                    Header = header,
                    Data = tableBuf
                };
                table.FieldOffsets = table.ReadRawOffsetTable();
                if (PackedTableIds.Contains(table.Header.TableId))
                {
                    var (packedOffsets, packedWidths) = table.ComputePackedLayout();
                    table.FieldOffsets = packedOffsets;
                    table.FieldBitWidths = packedWidths;
                }
                else
                {
                    table.FieldBitWidths = table.ComputeBitWidthsFromOffsets();
                }
                tables.Add(table);
            }
            catch { }
        }
        return tables;
    }

    private static List<int> FindTableStarts(byte[] payload)
    {
        var starts = new List<int>();
        for (var i = 0; i < payload.Length - 3; i++)
        {
            if (payload[i] == 0x53 && payload[i + 1] == 0x50 &&
                payload[i + 2] == 0x42 && payload[i + 3] == 0x46)
            {
                var start = i - TableStartOffset;
                if (start >= 0) starts.Add(start);
            }
        }
        return starts;
    }

    // Array tables (ASTO magic) live embedded between SPBF tables. Each one is a fixed
    // row set of 32-bit references (e.g. Team.Roster -> Player[] table 6097, "RosterStore").
    // The block extent is self-described: headerSize + recordCount*4 (arraySizes) +
    // recordCount*recordSize. Validation rejects string-pool false positives.
    public static List<FranchiseTable> ScanArrayTables(byte[] payload)
    {
        var results = new List<FranchiseTable>();
        var magic = "ASTO"u8.ToArray();
        for (var i = 0; i < payload.Length - 3; i++)
        {
            if (payload[i] != magic[0] || payload[i + 1] != magic[1] ||
                payload[i + 2] != magic[2] || payload[i + 3] != magic[3]) continue;
            var start = i - TableStartOffset;
            if (start < 0 || payload.Length - start < 200) continue;

            var buf = new byte[payload.Length - start];
            Array.Copy(payload, start, buf, 0, buf.Length);
            try
            {
                var header = ParseTableHeader(buf);
                if (!header.IsArray) continue;
                if (header.RecordCount <= 0 || header.RecordCount > 1_000_000) continue;
                if (header.RecordSize <= 0 || header.RecordSize > 1_000_000) continue;
                // Self-reference: the header's data1TableId must equal the block tableId.
                var ho = HeaderStart + 40 + header.TableStoreLength;
                if (ho + 8 >= buf.Length) continue;
                if (ReadU32BE(buf, ho + 4) != header.TableId) continue;
                var extent = header.Table2Start;
                if (extent <= 0 || extent > buf.Length) continue;

                var data = new byte[extent];
                Array.Copy(buf, 0, data, 0, extent);
                results.Add(new FranchiseTable
                {
                    AbsoluteStart = start,
                    AbsoluteEnd = start + extent,
                    Header = header,
                    Data = data
                });
            }
            catch { }
        }
        return results;
    }

    private static string ReadCString(byte[] buf, int offset)
    {
        var end = offset;
        while (end < buf.Length && buf[end] != 0) end++;
        return Encoding.Latin1.GetString(buf, offset, end - offset);
    }

    private static int ReadU32BE(byte[] buf, int offset) =>
        (buf[offset] << 24) | (buf[offset + 1] << 16) | (buf[offset + 2] << 8) | buf[offset + 3];

    private static FranchiseTableHeader ParseTableHeader(byte[] tableBuf)
    {
        var hs = HeaderStart;
        var name = ReadCString(tableBuf, 0);
        var isArray = name.Contains("[]");
        var tableId = ReadU32BE(tableBuf, hs);
        var uniqueId = ReadU32BE(tableBuf, hs + 4);
        var tableStoreLength = ReadU32BE(tableBuf, hs + 36);
        var headerOffset = hs + 40;
        if (tableStoreLength > 0) headerOffset += tableStoreLength;
        var recordCount = ReadU32BE(tableBuf, headerOffset + 8);
        var table1Length = ReadU32BE(tableBuf, headerOffset + 16);
        var table2Length = ReadU32BE(tableBuf, headerOffset + 20);
        var table3Length = ReadU32BE(tableBuf, headerOffset + 24);
        var tableTotalLength = ReadU32BE(tableBuf, headerOffset + 40);
        var recordWords = ReadU32BE(tableBuf, headerOffset + 44);
        var recordCapacity = ReadU32BE(tableBuf, headerOffset + 48);
        var numMembers = ReadU32BE(tableBuf, headerOffset + 52);
        var nextRecordToUse = ReadU32BE(tableBuf, headerOffset + 60);
        var offsetStart = 0xE8 + tableStoreLength;
        var recordSize = recordWords * 4;
        var headerSize = offsetStart;
        int table1Start, table2Start;
        if (!isArray)
        {
            headerSize += numMembers * 4;
            table1Start = headerSize;
            table2Start = headerSize + recordCount * recordSize;
        }
        else
        {
            table1Start = headerSize + recordCount * 4;
            table2Start = table1Start + recordCount * recordSize;
        }
        var table3Start = table2Start + table2Length;
        return new FranchiseTableHeader
        {
            Name = name, IsArray = isArray, TableId = tableId, UniqueId = uniqueId,
            RecordCount = recordCount, Table1Length = table1Length, Table2Length = table2Length,
            Table3Length = table3Length, RecordSize = recordSize, RecordCapacity = recordCapacity,
            NumMembers = numMembers, NextRecordToUse = nextRecordToUse,
            TableStoreLength = (int)tableStoreLength, RecordWords = (int)recordWords,
            OffsetStart = offsetStart, Table1Start = table1Start,
            Table2Start = table2Start, Table3Start = table3Start
        };
    }
}
