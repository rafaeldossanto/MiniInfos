using System.Buffers.Binary;
using System.IO;
using System.Windows.Media.Imaging;

namespace MiniInfos.Community;

public static class PackageImages
{
    public const int MaxPixels = 16_777_216;

    public static (int Width, int Height) Dimensions(string path)
    {
        if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new PackageException(L.Text("A imagem excede 32 MB."));
        if (!Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))
            throw new PackageException(L.Text("As imagens do pacote precisam estar em PNG."));
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[24];
        try { stream.ReadExactly(header); }
        catch (EndOfStreamException e) { throw new PackageException(L.Text("Imagem PNG incompleta."), e); }
        if (!header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) || !header.Slice(12, 4).SequenceEqual("IHDR"u8))
            throw new PackageException(L.Text("O arquivo não é uma imagem PNG válida."));
        var width = BinaryPrimitives.ReadInt32BigEndian(header[16..20]);
        var height = BinaryPrimitives.ReadInt32BigEndian(header[20..24]);
        if (width <= 0 || height <= 0 || width > 8192 || height > 8192 || (long)width * height > MaxPixels)
            throw new PackageException(L.Text("A imagem excede o limite de 16 milhões de pixels ou tem dimensões inválidas."));
        return (width, height);
    }

    public static BitmapSource Load(string path)
    {
        Dimensions(path);
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            frame.Freeze();
            return frame;
        }
        catch (Exception e) when (e is NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException)
        { throw new PackageException(L.Text("Não foi possível decodificar a imagem PNG."), e); }
    }

    public static byte[] Encode(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
