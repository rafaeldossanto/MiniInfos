using Avalonia.Media.Imaging;
using MiniInfos.Mascot;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace MiniInfos.Community;

public static class PackagePreviews
{
    public const int Size = 128;
    public static Bitmap Mascot(InstalledPackage package) => Mascot(PetAppearances.Load(package.Id, new PackageLibrary(Path.GetDirectoryName(Path.GetDirectoryName(package.Directory)!)!)).Frame(PetState.Idle, 0));
    public static Bitmap Mascot(PetFrame frame)
    {
        using var original = new SKBitmap(320, 320, SKColorType.Bgra8888, SKAlphaType.Premul);
        Marshal.Copy(frame.Pixels.ToArray(), 0, original.GetPixels(), 320 * 320 * 4);
        var bounds = PetPixels.Bounds(frame.Pixels.Span, 320);
        var scale = Math.Min(112 / bounds.Width, 112 / bounds.Height);
        using var small = new SKBitmap(Size, Size, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(small))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Translate((float)((Size - bounds.Width * scale) / 2 - bounds.X * scale), (float)((Size - bounds.Height * scale) / 2 - bounds.Y * scale));
            canvas.Scale((float)scale); canvas.DrawBitmap(original, 0, 0);
        }
        using var data = small.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = data.AsStream();
        return new Bitmap(stream);
    }
}
