using System.Text;

namespace MiniInfos.Providers.Antigravity;

/// <summary>A bounded protobuf wire reader for local usage metadata; unknown fields are skipped, never executed.</summary>
internal readonly ref struct MetadataFields
{
    private readonly ReadOnlySpan<byte> _bytes;
    public MetadataFields(ReadOnlySpan<byte> bytes) => _bytes = bytes;

    public ReadOnlySpan<byte> Message(int number)
    {
        var offset = 0;
        var found = ReadOnlySpan<byte>.Empty;
        while (Next(ref offset, out var field, out var wire, out var start, out var size, out _))
            if (field == number && wire == 2) found = _bytes.Slice(start, size);
        return found;
    }

    public long Count(int number)
    {
        var offset = 0;
        long found = 0;
        while (Next(ref offset, out var field, out var wire, out _, out _, out var value))
            if (field == number && wire == 0) found = value <= long.MaxValue ? (long)value : 0;
        return found;
    }

    public string? Text(int number)
    {
        var value = Message(number);
        if (value.Length is 0 or > 512) return null;
        var text = new UTF8Encoding(false, true).GetString(value);
        return text.Any(char.IsControl) ? null : text;
    }

    private bool Next(ref int offset, out int field, out int wire, out int start, out int size, out ulong value)
    {
        field = wire = start = size = 0; value = 0;
        if (offset == _bytes.Length) return false;
        var tag = Varint(ref offset);
        if (tag >> 3 is 0 or > 536870911) throw new FormatException("Invalid metadata field");
        field = (int)(tag >> 3); wire = (int)(tag & 7);
        if (wire == 0) { value = Varint(ref offset); return true; }
        var length = wire switch { 1 => 8UL, 5 => 4UL, 2 => Varint(ref offset), _ => throw new FormatException("Invalid metadata wire type") };
        if (length > (ulong)(_bytes.Length - offset)) throw new FormatException("Truncated metadata");
        start = offset; size = (int)length; offset += size;
        return true;
    }

    private ulong Varint(ref int offset)
    {
        ulong value = 0;
        for (var shift = 0; shift < 70; shift += 7)
        {
            if (offset >= _bytes.Length) break;
            var part = _bytes[offset++];
            if (shift == 63 && part > 1) break;
            value |= (ulong)(part & 127) << shift;
            if (part < 128) return value;
        }
        throw new FormatException("Invalid metadata varint");
    }
}
