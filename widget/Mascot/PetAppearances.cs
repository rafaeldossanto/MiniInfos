using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MiniInfos.Community;

namespace MiniInfos.Mascot;

public interface IPetAppearance
{
    string Name { get; }
    int FrameCount { get; }
    PetAnatomy Anatomy { get; }

    /// <summary>The visible center the character turns upright around after being let go, on the 320-unit canvas.</summary>
    Point RestCenter { get; }

    bool HasPose(PetState state);
    PetFrame Frame(PetState state, int index);
    PetPose PoseAt(PetState state, TimeSpan elapsed);
}

public sealed record BuiltInPet(string? Id, IPetAppearance Appearance);

public static class PetAppearances
{
    public static IPetAppearance Athena { get; } = new AthenaAppearance();
    /// <summary>The mascots that ship with the app; the owl, the default, has no id. Others come as packages.</summary>
    public static IReadOnlyList<BuiltInPet> BuiltIns { get; } = [new(null, Athena)];

    public static IPetAppearance? BuiltIn(string? id) => BuiltIns.FirstOrDefault(pet => pet.Id == id)?.Appearance;
    public static bool IsBuiltIn(string? id) => BuiltIn(id) is not null;

    public static IPetAppearance Load(string? id, PackageLibrary? library = null)
    {
        if (BuiltIn(id) is { } builtin) return builtin;
        var package = (library ?? PackageLibrary.Shared).Find(PackageKind.Mascot, id);
        if (package is null) return Athena;
        try { return new CommunityPet(package); }
        catch (Exception e) when (e is PackageException or IOException or UnauthorizedAccessException) { return Athena; }
    }

    private sealed class AthenaAppearance : IPetAppearance
    {
        public string Name => L.Text("Coruja de Atena");
        public int FrameCount => PetSprites.FrameCount;
        public PetAnatomy Anatomy => PetAnatomy.Athena;

        // The painted owl's body sits above the mass center used by her pendulum.
        public Point RestCenter => new(160, 200);

        public bool HasPose(PetState state) => true;
        public PetFrame Frame(PetState state, int index) => PetSprites.Frame(state, index);
        public PetPose PoseAt(PetState state, TimeSpan elapsed) => PetAnimation.PoseAt(state, elapsed);
    }

    private sealed class CommunityPet : IPetAppearance
    {
        private readonly MascotDefinition _definition;
        private readonly BitmapSource _sheet;
        private readonly Dictionary<(PetState, int), PetFrame> _frames = [];
        private sealed record Sequence(MascotTrack Definition, int[] Columns, int[] Durations)
        {
            public int Total { get; } = Durations.Sum();
        }
        private readonly Dictionary<PetState, Sequence> _tracks;
        public string Name { get; }
        public int FrameCount => _definition.Columns;
        public PetAnatomy Anatomy => _definition.Anatomy ?? PetAnatomy.Athena;
        public Point RestCenter => Anatomy.MassCenter.Point;

        public CommunityPet(InstalledPackage package)
        {
            Name = package.Name;
            _definition = package.Manifest.Mascot!;
            _sheet = PackageImages.Load(PackageFiles.Resolve(package.Directory, _definition.SpriteSheet));
            _tracks = _definition.States.ToDictionary(pair => pair.Key, pair =>
            {
                var columns = pair.Value.Frames ?? Enumerable.Range(0, _definition.Columns).ToArray();
                return new Sequence(pair.Value, columns, pair.Value.DurationsMs ?? Enumerable.Repeat(pair.Value.FrameDurationMs, columns.Length).ToArray());
            });
        }

        public bool HasPose(PetState state) => _definition.States.ContainsKey(state)
            || (state == PetState.HeldRightWing && _definition.States.ContainsKey(PetState.HeldLeftWing));

        private PetState SourceState(PetState state) => _definition.States.ContainsKey(state) ? state
            : state == PetState.HeldRightWing && HasPose(state) ? PetState.HeldLeftWing
            : state is PetState.HeldFeet or PetState.ProtectEyes && _definition.States.ContainsKey(PetState.Active) ? PetState.Active : PetState.Idle;

        public PetFrame Frame(PetState state, int index)
        {
            if (_frames.TryGetValue((state, index), out var found)) return found;
            var source = SourceState(state);
            var sequence = _tracks[source];
            var track = sequence.Definition;
            var columns = sequence.Columns;
            var column = columns[index % columns.Length];
            var width = _sheet.PixelWidth / _definition.Columns;
            var height = _sheet.PixelHeight / _definition.Rows;
            var cell = new CroppedBitmap(_sheet, new Int32Rect(column * width, track.Row * height, width, height));
            cell.Freeze();
            var frame = new PetFrame(cell, new Rect(0, 0, PetFrame.CanvasSize, PetFrame.CanvasSize));
            if (state == PetState.HeldFeet && !HasPose(state)) frame = frame.Transformed(new RotateTransform(180, 160, 160));
            if (state == PetState.HeldRightWing && source == PetState.HeldLeftWing) frame = frame.Transformed(new ScaleTransform(-1, 1, 160, 160));
            _frames[(state, index)] = frame;
            return frame;
        }

        public PetPose PoseAt(PetState state, TimeSpan elapsed)
        {
            var sequence = _tracks[SourceState(state)];
            var track = sequence.Definition;
            var count = sequence.Columns.Length;
            var duration = sequence.Durations;
            var time = Math.Max(0, elapsed.TotalMilliseconds) % sequence.Total;
            var index = 0;
            while (index < count - 1 && time >= duration[index]) time -= duration[index++];
            var blendFor = track.BlendMs == 0 ? duration[index] : Math.Min(track.BlendMs, duration[index]);
            return new(index, (index + 1) % count, PetAnimation.Ease((time - (duration[index] - blendFor)) / blendFor));
        }
    }
}
