using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MiniInfos;

/// <summary>A frame is drawn on a common canvas, centered and grounded, regardless of the owl's pose.</summary>
public sealed class PetFrame
{
    public const int CanvasSize = 320;
    private readonly Lazy<byte[]> _pixels;
    public ImageSource Source { get; }
    public ReadOnlyMemory<byte> Pixels => _pixels.Value;

    public PetFrame(BitmapSource bitmap, Rect destination)
    {
        Source = Draw(bitmap, destination);
        _pixels = new(() => Rasterize(Source));
    }

    public bool Hits(Point point)
    {
        if (point.X < 0 || point.X >= CanvasSize || point.Y < 0 || point.Y >= CanvasSize) return false;
        return _pixels.Value[((int)point.Y * CanvasSize + (int)point.X) * 4 + 3] >= 32;
    }

    private static byte[] Rasterize(ImageSource source)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen()) context.DrawImage(source, new Rect(0, 0, CanvasSize, CanvasSize));
        var bitmap = new RenderTargetBitmap(CanvasSize, CanvasSize, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var pixels = new byte[CanvasSize * CanvasSize * 4];
        bitmap.CopyPixels(pixels, CanvasSize * 4, 0);
        return pixels;
    }

    private static ImageSource Draw(BitmapSource bitmap, Rect destination)
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, CanvasSize, CanvasSize));
            context.DrawImage(bitmap, destination);
        }
        drawing.Freeze();
        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }
}

/// <summary>The generated atlas is decoded once; frames are cropped at runtime, not copied to disk.</summary>
public static class PetSprites
{
    private sealed record Slice(BitmapSource Bitmap, double FootCenter);
    public const int FrameCount = 8;
    private static readonly Lazy<PetFrame[][]> Frames = new(Load);

    public static PetFrame Frame(PetState state, int index) => Frames.Value[(int)state][index % FrameCount];

    private static PetFrame[][] Load()
    {
        var original = new BitmapImage(new Uri("pack://application:,,,/MiniInfos;component/Assets/athena-owl-smooth.png"));
        var sheet = new FormatConvertedBitmap(original, PixelFormats.Bgra32, null, 0);
        var cells = Enumerable.Range(0, 4).Select(row => Enumerable.Range(0, FrameCount).Select(column =>
        {
            var x = sheet.PixelWidth * column / FrameCount;
            var end = sheet.PixelWidth * (column + 1) / FrameCount;
            var y = sheet.PixelHeight * row / 4;
            return Trim(sheet, new Int32Rect(x, y, end - x, sheet.PixelHeight * (row + 1) / 4 - y));
        }).ToArray()).ToArray();
        // Keep the owl's screen size when the source atlas resolution changes.
        var scale = 224.0 / cells[0][0].Bitmap.PixelHeight;
        return cells.Select(row => row.Select(slice => new PetFrame(slice.Bitmap,
            new Rect(PetFrame.CanvasSize / 2.0 - slice.FootCenter * scale,
                PetFrame.CanvasSize - 20 - slice.Bitmap.PixelHeight * scale,
                slice.Bitmap.PixelWidth * scale, slice.Bitmap.PixelHeight * scale))).ToArray()).ToArray();
    }

    private static Slice Trim(BitmapSource sheet, Int32Rect cell)
    {
        var stride = cell.Width * 4;
        var pixels = new byte[stride * cell.Height];
        sheet.CopyPixels(cell, pixels, stride, 0);
        var left = cell.Width; var right = 0; var top = cell.Height; var bottom = 0;
        for (var y = 0; y < cell.Height; y++)
        for (var x = 0; x < cell.Width; x++)
        {
            if (pixels[y * stride + x * 4 + 3] < 32) continue;
            left = Math.Min(left, x); right = Math.Max(right, x + 1);
            top = Math.Min(top, y); bottom = Math.Max(bottom, y + 1);
        }

        if (right <= left || bottom <= top) throw new InvalidDataException("The owl atlas contains an empty frame.");
        // Register on the talons, not the whole silhouette: an asymmetric wing must not slide the body sideways.
        double weightedX = 0, weight = 0;
        for (var y = Math.Max(top, bottom - (bottom - top) / 10); y < bottom; y++)
        for (var x = left; x < right; x++)
        {
            var alpha = pixels[y * stride + x * 4 + 3];
            if (alpha < 128) continue;
            weightedX += (x + 0.5) * alpha;
            weight += alpha;
        }
        var footCenter = weight > 0 ? weightedX / weight : (left + right) / 2.0;
        var bitmap = new CroppedBitmap(sheet, new Int32Rect(cell.X + left, cell.Y + top, right - left, bottom - top));
        bitmap.Freeze();
        return new Slice(bitmap, footCenter - left);
    }
}
