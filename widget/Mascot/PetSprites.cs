using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;

namespace MiniInfos.Mascot;

/// <summary>A frame is drawn on a common canvas, centered and grounded, regardless of the character's pose.</summary>
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
        var cells = PetAtlas.Read("athena-owl-smooth.png");
        var scale = 224.0 / cells[0][0].Bitmap.PixelHeight;
        return cells.Select(row => row.Select(slice => PetAtlas.Grounded(slice, scale)).ToArray()).ToArray();
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
        var cells = PetAtlas.Read("athena-owl-interactions.png", rows, columns);
        var scale = 224.0 / cells[0][0].Bitmap.PixelHeight;
        return cells.Select((row, index) => row.Select(slice =>
        {
            if (index == 0) return PetAtlas.Grounded(slice, scale);
            var state = index == 1 ? PetState.HeldHead : index == 2 ? PetState.HeldLeftWing : PetState.HeldBody;
            var pivot = index == 3 ? new Point(slice.FootCenter, slice.Bitmap.PixelHeight * 0.63) : slice.TopCenter;
            var target = PetInteraction.AnchorFor(state);
            return new PetFrame(slice.Bitmap, new Rect(target.X - pivot.X * scale, target.Y - pivot.Y * scale,
                slice.Bitmap.PixelWidth * scale, slice.Bitmap.PixelHeight * scale));
        }).ToArray()).ToArray();
    }
}
