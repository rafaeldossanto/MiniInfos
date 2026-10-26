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

    private PetFrame(ImageSource source)
    {
        Source = source;
        _pixels = new(() => Rasterize(Source));
    }

    public PetFrame(BitmapSource bitmap, Rect destination) : this(Draw(bitmap, destination)) { }

    public PetFrame Transformed(Transform transform)
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, CanvasSize, CanvasSize));
            context.PushTransform(transform);
            context.DrawImage(Source, new Rect(0, 0, CanvasSize, CanvasSize));
            context.Pop();
        }
        drawing.Freeze();
        var source = new DrawingImage(drawing);
        source.Freeze();
        return new PetFrame(source);
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
    private sealed record Slice(BitmapSource Bitmap, double FootCenter, Point TopCenter);
    public const int FrameCount = 8;
    private static readonly Lazy<PetFrame[][]> Frames = new(Load);
    private static readonly Lazy<PetFrame[][]> Interactions = new(LoadInteractions);
    private static readonly Lazy<PetFrame[]> RightWing = new(() => Interactions.Value[2]
        .Select(frame => frame.Transformed(new ScaleTransform(-1, 1, 160, 160))).ToArray());
    private static readonly Lazy<PetFrame[]> Feet = new(() => Frames.Value[(int)PetState.Active]
        .Select(frame => frame.Transformed(new RotateTransform(180, 160, 160))).ToArray());

    public static PetFrame Frame(PetState state, int index)
    {
        var frames = state switch
        {
            PetState.ProtectEyes => Interactions.Value[0],
            PetState.HeldHead => Interactions.Value[1],
            PetState.HeldLeftWing => Interactions.Value[2],
            PetState.HeldRightWing => RightWing.Value,
            PetState.HeldBody => Interactions.Value[3],
            PetState.HeldFeet => Feet.Value,
            _ => Frames.Value[(int)state],
        };
        return frames[index % FrameCount];
    }

    private static PetFrame[][] Load()
    {
        var cells = ReadAtlas("athena-owl-smooth.png");
        var scale = 224.0 / cells[0][0].Bitmap.PixelHeight;
        return cells.Select(row => row.Select(slice => Grounded(slice, scale)).ToArray()).ToArray();
    }

    private static PetFrame[][] LoadInteractions()
    {
        // The generated rows have different padding; boundaries fall in their transparent gaps.
        int[] rows = [0, 225, 442, 687, 887];
        int[][] columns =
        [
            [0, 244, 475, 690, 907, 1114, 1317, 1547, 1774],
            [0, 236, 457, 679, 903, 1119, 1320, 1541, 1774],
            [0, 250, 477, 697, 912, 1125, 1339, 1557, 1774],
            [0, 244, 461, 686, 906, 1123, 1337, 1551, 1774],
        ];
        var cells = ReadAtlas("athena-owl-interactions.png", rows, columns);
        var scale = 224.0 / cells[0][0].Bitmap.PixelHeight;
        return cells.Select((row, index) => row.Select(slice =>
        {
            if (index == 0) return Grounded(slice, scale);
            var state = index == 1 ? PetState.HeldHead : index == 2 ? PetState.HeldLeftWing : PetState.HeldBody;
            var pivot = index == 3 ? new Point(slice.FootCenter, slice.Bitmap.PixelHeight * 0.63) : slice.TopCenter;
            var target = PetInteraction.AnchorFor(state);
            return new PetFrame(slice.Bitmap, new Rect(target.X - pivot.X * scale, target.Y - pivot.Y * scale,
                slice.Bitmap.PixelWidth * scale, slice.Bitmap.PixelHeight * scale));
        }).ToArray()).ToArray();
    }

    private static PetFrame Grounded(Slice slice, double scale) => new(slice.Bitmap,
        new Rect(PetFrame.CanvasSize / 2.0 - slice.FootCenter * scale,
            PetFrame.CanvasSize - 20 - slice.Bitmap.PixelHeight * scale,
            slice.Bitmap.PixelWidth * scale, slice.Bitmap.PixelHeight * scale));

    private static Slice[][] ReadAtlas(string name, int[]? rows = null, int[][]? columns = null)
    {
        var original = new BitmapImage(new Uri($"pack://application:,,,/MiniInfos;component/Assets/{name}"));
        var sheet = new FormatConvertedBitmap(original, PixelFormats.Bgra32, null, 0);
        return Enumerable.Range(0, 4).Select(row => Enumerable.Range(0, FrameCount).Select(column =>
        {
            var x = columns?[row][column] ?? sheet.PixelWidth * column / FrameCount;
            var end = columns?[row][column + 1] ?? sheet.PixelWidth * (column + 1) / FrameCount;
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
