namespace PinkSlipsTool.Models;

public static class RecordCodec
{
    public static int ReadBits(byte[] record, int bitOffset, int length)
    {
        var value = 0;
        for (var b = 0; b < length; b++)
        {
            var byteIdx = (bitOffset + b) / 8;
            var bitIdx = 7 - ((bitOffset + b) % 8);
            var bit = (record[byteIdx] >> bitIdx) & 1;
            value = (value << 1) | bit;
        }
        return value;
    }

    public static int ReadSignedBits(byte[] record, int bitOffset, int length)
    {
        var raw = ReadBits(record, bitOffset, length);
        if (length < 2) return raw;
        var signBit = 1 << (length - 1);
        if ((raw & signBit) != 0)
            raw -= (1 << length);
        return raw;
    }

    public static string ReadCStringAt(byte[] buf, int offset)
    {
        var end = offset;
        while (end < buf.Length && buf[end] != 0) end++;
        return System.Text.Encoding.Latin1.GetString(buf, offset, end - offset);
    }

    public static void WriteBits(byte[] record, int bitOffset, int length, int value)
    {
        for (var b = 0; b < length; b++)
        {
            var byteIdx = (bitOffset + b) / 8;
            var bitIdx = 7 - ((bitOffset + b) % 8);
            var bitVal = (value >> (length - 1 - b)) & 1;
            if (bitVal == 1)
                record[byteIdx] |= (byte)(1 << bitIdx);
            else
                record[byteIdx] &= (byte)~(1 << bitIdx);
        }
    }
}
