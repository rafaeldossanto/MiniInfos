using Avalonia;
using Avalonia.Platform;
using SkiaSharp;
using MiniInfos.Community;
using System.Runtime.InteropServices;

namespace MiniInfos.Mascot;

/// <summary>The native renderer supplies pixels; timing, anatomy, gestures and physics are shared with WPF.</summary>
public sealed class PetFrame
{
    public const int CanvasSize = 320;
    public ReadOnlyMemory<byte> Pixels { get; }
    public PetFrame(SKBitmap image, Rect destination)
    {
        using var canvas = new SKBitmap(CanvasSize, CanvasSize, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var draw = new SKCanvas(canvas))
        {
            draw.Clear(SKColors.Transparent);
            draw.DrawBitmap(image, new SKRect((float)destination.X, (float)destination.Y, (float)destination.Right, (float)destination.Bottom));
        }
        Pixels = Bytes(canvas);
    }
    private PetFrame(byte[] pixels) => Pixels = pixels;
    internal static byte[] Bytes(SKBitmap bitmap)
    {
        var data = new byte[bitmap.Width * bitmap.Height * 4];
        for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(bitmap.GetPixels() + y * bitmap.RowBytes, data, y * bitmap.Width * 4, bitmap.Width * 4);
        return data;
    }
    public PetFrame Turn(bool mirror = false)
    {
        var data = Pixels.ToArray();
        if (!mirror) PetPixels.HalfTurn(data);
        else for (var y = 0; y < CanvasSize; y++)
            for (var x = 0; x < CanvasSize; x++)
                Pixels.Span.Slice((y * CanvasSize + x) * 4, 4).CopyTo(data.AsSpan((y * CanvasSize + CanvasSize - x - 1) * 4, 4));
        return new(data);
    }
    public bool Hits(Point point) => point.X >= 0 && point.X < CanvasSize && point.Y >= 0 && point.Y < CanvasSize
        && Pixels.Span[((int)point.Y * CanvasSize + (int)point.X) * 4 + 3] >= 32;
}

public static class PetSprites
{
    public const int FrameCount = 8;
    private static readonly Lazy<PetFrame[][]> Normal = new(() => Read("athena-owl-smooth.png"));
    private static readonly Lazy<PetFrame[][]> Held = new(() => Read("athena-owl-interactions.png", true));
    private static readonly Lazy<PetFrame[]> Right = new(() => Held.Value[2].Select(frame => frame.Turn(true)).ToArray());
    private static readonly Lazy<PetFrame[]> Feet = new(() => Normal.Value[1].Select(frame => frame.Turn()).ToArray());
    public static PetFrame Frame(PetState state, int index) => (state switch
    {
        PetState.ProtectEyes => Held.Value[0], PetState.HeldHead => Held.Value[1],
        PetState.HeldLeftWing => Held.Value[2], PetState.HeldRightWing => Right.Value,
        PetState.HeldBody => Held.Value[3], PetState.HeldFeet => Feet.Value,
        _ => Normal.Value[(int)state],
    })[index % FrameCount];

    private static PetFrame[][] Read(string name, bool interaction = false)
    {
        using var stream = AssetLoader.Open(new Uri("avares://MiniInfos.Desktop/Assets/" + name));
        using var source = SKBitmap.Decode(stream) ?? throw new InvalidDataException("Invalid mascot artwork.");
        int[] rows = [0, 225, 442, 687, 887];
        int[][] columns = [[0,244,475,690,907,1114,1317,1547,1774], [0,236,457,679,903,1119,1320,1541,1774],
            [0,250,477,697,912,1125,1339,1557,1774], [0,244,461,686,906,1123,1337,1551,1774]];
        var trimmed = new (SKBitmap Image, double Foot, Point Top)[4][];
        for (var row = 0; row < 4; row++)
        {
            trimmed[row] = new (SKBitmap, double, Point)[FrameCount];
            for (var col = 0; col < FrameCount; col++)
            {
                var x = interaction ? columns[row][col] : source.Width * col / FrameCount;
                var right = interaction ? columns[row][col + 1] : source.Width * (col + 1) / FrameCount;
                var y = interaction ? rows[row] : source.Height * row / 4;
                var bottom = interaction ? rows[row + 1] : source.Height * (row + 1) / 4;
                trimmed[row][col] = Trim(source, new SKRectI(x, y, right, bottom));
            }
        }
        var scale = 224.0 / trimmed[0][0].Image.Height;
        return trimmed.Select((row, index) => row.Select(slice =>
        {
            using var image = slice.Image;
            var destination = new Rect(160 - slice.Foot * scale, 300 - image.Height * scale, image.Width * scale, image.Height * scale);
            if (interaction && index > 0)
            {
                var state = index == 1 ? PetState.HeldHead : index == 2 ? PetState.HeldLeftWing : PetState.HeldBody;
                var pivot = index == 3 ? new Point(slice.Foot, image.Height * 0.63) : slice.Top;
                var target = PetInteraction.AnchorFor(state);
                destination = new Rect(target.X - pivot.X * scale, target.Y - pivot.Y * scale, image.Width * scale, image.Height * scale);
            }
            return new PetFrame(image, destination);
        }).ToArray()).ToArray();
    }

    private static (SKBitmap, double, Point) Trim(SKBitmap sheet, SKRectI cell)
    {
        var left = cell.Right; var right = cell.Left; var top = cell.Bottom; var bottom = cell.Top;
        for (var y = cell.Top; y < cell.Bottom; y++)
        for (var x = cell.Left; x < cell.Right; x++)
            if (sheet.GetPixel(x, y).Alpha >= 32) { left = Math.Min(left, x); right = Math.Max(right, x + 1); top = Math.Min(top, y); bottom = Math.Max(bottom, y + 1); }
        if (right <= left || bottom <= top) throw new InvalidDataException("Empty mascot frame.");
        double feet = 0, weight = 0, topX = 0, topY = 0, topWeight = 0;
        for (var y = Math.Max(top, bottom - (bottom - top) / 10); y < bottom; y++)
        for (var x = left; x < right; x++)
        {
            var alpha = sheet.GetPixel(x, y).Alpha;
            if (alpha < 128) continue;
            feet += (x + 0.5) * alpha; weight += alpha;
        }
        for (var y = top; y < Math.Min(bottom, top + 14); y++)
        for (var x = left; x < right; x++)
        {
            var alpha = sheet.GetPixel(x, y).Alpha;
            if (alpha < 128) continue;
            topX += (x + 0.5) * alpha; topY += (y + 0.5) * alpha; topWeight += alpha;
        }
        var copy = new SKBitmap(right - left, bottom - top, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(copy)) canvas.DrawBitmap(sheet, new SKRect(left, top, right, bottom), new SKRect(0, 0, copy.Width, copy.Height));
        return (copy, (weight > 0 ? feet / weight : (left + right) / 2.0) - left,
            topWeight > 0 ? new Point(topX / topWeight - left, topY / topWeight - top) : new Point(copy.Width / 2.0, 0));
    }
}

public interface IPetAppearance
{
    string Name { get; }
    PetAnatomy Anatomy { get; }
    Point RestCenter { get; }
    bool HasPose(PetState state);
    PetFrame Frame(PetState state, int index);
    PetPose PoseAt(PetState state, TimeSpan elapsed);
}

public static class PetAppearances
{
    public static IPetAppearance Athena { get; } = new Owl();
    public static IPetAppearance? BuiltIn(string? id) => id is null ? Athena : null;
    public static IPetAppearance Load(string? id, PackageLibrary? library = null)
    {
        var package = (library ?? PackageLibrary.Shared).Find(PackageKind.Mascot, id);
        return package is null ? Athena : new CommunityAppearance(package);
    }
    private sealed class Owl : IPetAppearance
    {
        public string Name => L.Text("Coruja de Atena");
        public PetAnatomy Anatomy => PetAnatomy.Athena;
        public Point RestCenter => new(160, 200);
        public bool HasPose(PetState state) => true;
        public PetFrame Frame(PetState state, int index) => PetSprites.Frame(state, index);
        public PetPose PoseAt(PetState state, TimeSpan elapsed) => PetAnimation.PoseAt(state, elapsed);
    }
    private sealed class CommunityAppearance : IPetAppearance
    {
        private readonly MascotDefinition _definition;
        private readonly SKBitmap _sheet;
        private readonly Dictionary<(PetState, int), PetFrame> _frames = [];
        public string Name { get; }
        public PetAnatomy Anatomy => _definition.Anatomy ?? PetAnatomy.Athena;
        public Point RestCenter => Anatomy.MassCenter.Point;
        public CommunityAppearance(InstalledPackage package)
        {
            _definition = package.Manifest.Mascot!; Name = package.Name;
            _sheet = SKBitmap.Decode(PackageFiles.Resolve(package.Directory, _definition.SpriteSheet)) ?? throw new PackageException("Invalid PNG.");
        }
        public bool HasPose(PetState state) => _definition.States.ContainsKey(state) || state == PetState.HeldRightWing && _definition.States.ContainsKey(PetState.HeldLeftWing);
        private PetState SourceState(PetState state) => _definition.States.ContainsKey(state) ? state
            : state == PetState.HeldRightWing && HasPose(state) ? PetState.HeldLeftWing
            : state is PetState.HeldFeet or PetState.ProtectEyes && _definition.States.ContainsKey(PetState.Active) ? PetState.Active : PetState.Idle;
        public PetFrame Frame(PetState state, int index)
        {
            if (_frames.TryGetValue((state, index), out var cached)) return cached;
            var source = SourceState(state); var track = _definition.States[source];
            var columns = track.Frames ?? Enumerable.Range(0, _definition.Columns).ToArray();
            var col = columns[index % columns.Length];
            var width = _sheet.Width / _definition.Columns; var height = _sheet.Height / _definition.Rows;
            using var cell = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(cell)) canvas.DrawBitmap(_sheet, new SKRect(col * width, track.Row * height, (col + 1) * width, (track.Row + 1) * height), new SKRect(0, 0, width, height));
            var frame = new PetFrame(cell, new Rect(0, 0, 320, 320));
            if (state == PetState.HeldFeet && !HasPose(state)) frame = frame.Turn();
            if (state == PetState.HeldRightWing && source == PetState.HeldLeftWing) frame = frame.Turn(true);
            return _frames[(state, index)] = frame;
        }
        public PetPose PoseAt(PetState state, TimeSpan elapsed)
        {
            var track = _definition.States[SourceState(state)];
            var count = track.Frames?.Length ?? _definition.Columns;
            var durations = track.DurationsMs ?? Enumerable.Repeat(track.FrameDurationMs, count).ToArray();
            var time = Math.Max(0, elapsed.TotalMilliseconds) % durations.Sum(); var index = 0;
            while (index < count - 1 && time >= durations[index]) time -= durations[index++];
            var blend = track.BlendMs == 0 ? durations[index] : Math.Min(track.BlendMs, durations[index]);
            return new(index, (index + 1) % count, PetAnimation.Ease((time - (durations[index] - blend)) / blend));
        }
    }
}
