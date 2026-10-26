using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MiniInfos;

/// <summary>A frame is drawn on a common canvas, centered and grounded, regardless of the owl's pose.</summary>
public sealed class PetFrame(BitmapSource bitmap, byte[] pixels, int stride, Rect destination)
{
    public const double CanvasSize = 320;
    public ImageSource Source { get; } = Draw(bitmap, destination);

    public bool Hits(Point point)
    {
        if (!destination.Contains(point)) return false;
        var x = Math.Clamp((int)(point.X - destination.X), 0, bitmap.PixelWidth - 1);
        var y = Math.Clamp((int)(point.Y - destination.Y), 0, bitmap.PixelHeight - 1);
        return pixels[y * stride + x * 4 + 3] >= 32;
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
    private static readonly Lazy<PetFrame[][]> Frames = new(Load);

    public static PetFrame Frame(PetState state, int index) => Frames.Value[(int)state][index % 4];

    public static int IndexAt(PetState state, TimeSpan elapsed)
    {
        var milliseconds = elapsed.TotalMilliseconds;
        if (state == PetState.Idle)
        {
            var blink = milliseconds % 5000;
            return blink < 4550 ? 0 : blink < 4700 ? 1 : blink < 4850 ? 2 : 3;
        }
        var duration = state == PetState.Active ? 140 : state == PetState.Alert ? 350 : 600;
        return (int)(milliseconds / duration) % 4;
    }

    private static PetFrame[][] Load()
    {
        var original = new BitmapImage(new Uri("pack://application:,,,/MiniInfos;component/Assets/athena-owl.png"));
        var sheet = new FormatConvertedBitmap(original, PixelFormats.Bgra32, null, 0);
        // Transparent gaps in this atlas separate the four animation strips.
        int[] rows = [0, 362, 670, 963, sheet.PixelHeight];
        return Enumerable.Range(0, 4).Select(row => Enumerable.Range(0, 4).Select(column =>
        {
            var x = sheet.PixelWidth * column / 4;
            var end = sheet.PixelWidth * (column + 1) / 4;
            return ReadFrame(sheet, new Int32Rect(x, rows[row], end - x, rows[row + 1] - rows[row]));
        }).ToArray()).ToArray();
    }

    private static PetFrame ReadFrame(BitmapSource sheet, Int32Rect cell)
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
        var bitmap = new CroppedBitmap(sheet, new Int32Rect(cell.X + left, cell.Y + top, right - left, bottom - top));
        bitmap.Freeze();
        stride = bitmap.PixelWidth * 4;
        pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        var target = new Rect((PetFrame.CanvasSize - bitmap.PixelWidth) / 2, PetFrame.CanvasSize - 20 - bitmap.PixelHeight,
            bitmap.PixelWidth, bitmap.PixelHeight);
        return new PetFrame(bitmap, pixels, stride, target);
    }
}
