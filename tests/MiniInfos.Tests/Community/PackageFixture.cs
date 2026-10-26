using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using MiniInfos.Community;

namespace MiniInfos.Tests.Community;

internal static class PackageFixture
{
    public static PackageManifest Theme(string id = "test.ocean", string version = "1.0.0") => new(
        1, PackageKind.Theme, id, "Oceano", "Comunidade", version, "CC0-1.0",
        Theme: new(Colors: new() { ["Accent"] = "#00B8D4", ["CardBackground"] = "#18242B" }));

    public static PackageManifest Mascot(string id = "test.friend") => new(
        1, PackageKind.Mascot, id, "Companheiro", "Comunidade", "1.0.0", "CC0-1.0",
        Mascot: new("sprites.png", 2, 1, new() { [MiniInfos.Mascot.PetState.Idle] = new(0, Frames: [0, 1], DurationsMs: [80, 240], BlendMs: 0) }));

    public static byte[] Png(int width = 2, int height = 1)
    {
        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; header[9] = 6;
        Chunk("IHDR", header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            for (var row = 0; row < height; row++)
            {
                zlib.WriteByte(0);
                for (var col = 0; col < width; col++) zlib.Write([20, 180, 220, 255]);
            }
        Chunk("IDAT", compressed.ToArray()); Chunk("IEND", []);
        return png.ToArray();

        void Chunk(string type, byte[] bytes)
        {
            Span<byte> number = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(number, bytes.Length); png.Write(number);
            var data = Encoding.ASCII.GetBytes(type).Concat(bytes).ToArray();
            png.Write(data);
            uint crc = 0xffffffff;
            foreach (var value in data)
            {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
            }
            BinaryPrimitives.WriteUInt32BigEndian(number, ~crc); png.Write(number);
        }
    }
}
