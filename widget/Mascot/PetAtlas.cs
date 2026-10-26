using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MiniInfos.Mascot;

/// <summary>Shared trimming and registration for embedded sprite atlases, including artwork-specific gutter boundaries.</summary>
internal static class PetAtlas
{
    internal sealed record Slice(BitmapSource Bitmap, double FootCenter, Point TopCenter);

    public static PetFrame Grounded(Slice slice, double scale) => new(slice.Bitmap,
        new Rect(PetFrame.CanvasSize / 2.0 - slice.FootCenter * scale,
            PetFrame.CanvasSize - 20 - slice.Bitmap.PixelHeight * scale,
            slice.Bitmap.PixelWidth * scale, slice.Bitmap.PixelHeight * scale));

    public static Slice[][] Read(string name, int[]? rows = null, int[][]? columns = null)
    {
        _ = Application.ResourceAssembly;
        var original = new BitmapImage(new Uri($"pack://application:,,,/MiniInfos;component/Assets/{name}"));
        var sheet = new FormatConvertedBitmap(original, PixelFormats.Bgra32, null, 0);
        return Enumerable.Range(0, 4).Select(row => Enumerable.Range(0, PetSprites.FrameCount).Select(column =>
        {
            var x = columns?[row][column] ?? sheet.PixelWidth * column / PetSprites.FrameCount;
            var end = columns?[row][column + 1] ?? sheet.PixelWidth * (column + 1) / PetSprites.FrameCount;
            var y = rows?[row] ?? sheet.PixelHeight * row / 4;
            var bottom = rows?[row + 1] ?? sheet.PixelHeight * (row + 1) / 4;
            return Trim(sheet, new Int32Rect(x, y, end - x, bottom - y));
        }).ToArray()).ToArray();
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

        if (right <= left || bottom <= top) throw new InvalidDataException("The mascot atlas contains an empty frame.");
        // Register on the feet, not the whole silhouette: an asymmetric limb must not slide the body sideways.
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
        double topX = 0, topY = 0, topWeight = 0;
        for (var y = top; y < Math.Min(bottom, top + 14); y++)
        for (var x = left; x < right; x++)
        {
            var alpha = pixels[y * stride + x * 4 + 3];
            if (alpha < 128) continue;
            topX += (x + 0.5) * alpha; topY += (y + 0.5) * alpha; topWeight += alpha;
        }
        var topCenter = topWeight > 0 ? new Point(topX / topWeight - left, topY / topWeight - top)
            : new Point((right - left) / 2.0, 0);
        var bitmap = new CroppedBitmap(sheet, new Int32Rect(cell.X + left, cell.Y + top, right - left, bottom - top));
        bitmap.Freeze();
        return new Slice(bitmap, footCenter - left, topCenter);
    }
}
