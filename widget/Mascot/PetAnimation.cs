using System.Runtime.InteropServices;

namespace MiniInfos.Mascot;

public readonly record struct PetPose(int From, int To, double Blend);
public readonly record struct PetMotion(double X, double Y, double ScaleX, double ScaleY, double Angle);

/// <summary>Elapsed-time tracks: playback stays smooth even when the renderer misses a frame.</summary>
public static class PetAnimation
{
    public static readonly TimeSpan StateTransition = TimeSpan.FromMilliseconds(300);
    private static readonly int[] IdleDurations = [3500, 260, 240, 95, 55, 95, 360, 395];
    private static readonly int[] ActiveDurations = [130, 100, 90, 90, 110, 100, 110, 150];
    private static readonly int[] ProtectionDurations = [90, 100, 110, 180, 300, 200, 180, 290];
    private static readonly int[] SleepDurations = Enumerable.Repeat(450, PetSprites.FrameCount).ToArray();
    private static readonly int[] OtherDurations = Enumerable.Repeat(260, PetSprites.FrameCount).ToArray();

    /// <summary>How long each of the owl's poses lasts in a state: what she plays, and what an exported model starts from.</summary>
    public static IReadOnlyList<int> DurationsFor(PetState state) => state switch
    {
        PetState.Idle => IdleDurations, PetState.Active => ActiveDurations,
        PetState.ProtectEyes => ProtectionDurations, PetState.Sleep => SleepDurations, _ => OtherDurations,
    };

    public static PetPose PoseAt(PetState state, TimeSpan elapsed)
    {
        var durations = DurationsFor(state);
        var local = Math.Max(0, elapsed.TotalMilliseconds) % durations.Sum();
        var index = 0;
        while (index < PetSprites.FrameCount - 1 && local >= durations[index])
        {
            local -= durations[index];
            index++;
        }

        var duration = durations[index];
        var blendFor = state == PetState.Idle ? Math.Min(duration, 160) : duration;
        var progress = Math.Clamp((local - (duration - blendFor)) / blendFor, 0, 1);
        return new(index, (index + 1) % PetSprites.FrameCount, Ease(progress));
    }

    /// <summary>Breathing and a small sway, in proportions of the window; feet remain almost grounded.</summary>
    public static PetMotion MotionAt(TimeSpan elapsed)
    {
        var seconds = elapsed.TotalSeconds;
        var breath = Math.Sin(seconds * Math.Tau / 3.6);
        var sway = Math.Sin(seconds * Math.Tau / 6.2);
        return new(0.003 * sway, -0.006 * breath, 1 - 0.005 * breath, 1 + 0.014 * breath, 0.65 * sway);
    }

    public static double Ease(double progress) => (1 - Math.Cos(Math.PI * Math.Clamp(progress, 0, 1))) / 2;
}

/// <summary>Interpolates premultiplied pixels, retaining opacity instead of stacking two translucent images.</summary>
public static class PetPixels
{
    public static void HalfTurn(Span<byte> pixels)
    {
        if (pixels.Length % 4 != 0) throw new ArgumentException("Pet pixels must use BGRA buffers.");
        MemoryMarshal.Cast<byte, uint>(pixels).Reverse();
    }

    public static System.Windows.Rect Bounds(ReadOnlySpan<byte> pixels, int size)
    {
        if (pixels.Length != size * size * 4) throw new ArgumentException("Pet pixels do not match the canvas.");
        var left = size; var top = size; var right = 0; var bottom = 0;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            if (pixels[(y * size + x) * 4 + 3] < 32) continue;
            left = Math.Min(left, x); right = Math.Max(right, x + 1);
            top = Math.Min(top, y); bottom = Math.Max(bottom, y + 1);
        }
        return right > left ? new(left, top, right - left, bottom - top) : new(0, 0, size, size);
    }

    public static int Weight(double amount) => (int)Math.Round(Math.Clamp(amount, 0, 1) * 256);

    public static void Mix(ReadOnlySpan<byte> from, ReadOnlySpan<byte> to, Span<byte> output, int weight)
    {
        if (from.Length != to.Length || from.Length != output.Length || output.Length % 4 != 0)
            throw new ArgumentException("Pet frames must have matching premultiplied BGRA buffers.");
        if (weight <= 0) { from.CopyTo(output); return; }
        if (weight >= 256) { to.CopyTo(output); return; }
        var first = MemoryMarshal.Cast<byte, uint>(from);
        var second = MemoryMarshal.Cast<byte, uint>(to);
        var target = MemoryMarshal.Cast<byte, uint>(output);
        var next = (uint)weight;
        var current = 256 - next;
        for (var index = 0; index < target.Length; index++)
        {
            var a = first[index]; var b = second[index];
            var rb = (((a & 0x00FF00FF) * current + (b & 0x00FF00FF) * next + 0x00800080) >> 8) & 0x00FF00FF;
            var ga = (((((a >> 8) & 0x00FF00FF) * current + ((b >> 8) & 0x00FF00FF) * next + 0x00800080) >> 8) & 0x00FF00FF) << 8;
            target[index] = rb | ga;
        }
    }
}

/// <summary>Blends authored poses and eases state changes from the actual last displayed pixels.</summary>
public sealed class PetAnimationPlayer(Func<PetState, int, ReadOnlyMemory<byte>> frame, int byteCount,
    Func<PetState, TimeSpan, PetPose>? poseAt = null)
{
    private readonly byte[] _pixels = new byte[byteCount];
    private readonly byte[] _target = new byte[byteCount];
    private readonly byte[] _previous = new byte[byteCount];
    private PetState? _state;
    private int _revision;
    private TimeSpan _stateStarted;
    private (PetState State, int From, int To, int Pose, int Transition)? _last;
    private bool _transitioning;

    public ReadOnlyMemory<byte> Pixels => _pixels;
    internal byte[] Buffer => _pixels;

    /// <summary>Rebase an upside-down hold into the normal artwork orientation without changing its on-screen pose.</summary>
    public void HalfTurn()
    {
        PetPixels.HalfTurn(_pixels);
        _last = null;
    }

    public bool Draw(PetState state, TimeSpan elapsed, int revision = 0)
    {
        if (_state != state || _revision != revision)
        {
            _transitioning = _state is not null;
            if (_transitioning) _pixels.CopyTo(_previous, 0);
            _state = state;
            _revision = revision;
            _stateStarted = elapsed;
            _last = null;
        }
        var local = elapsed - _stateStarted;
        var pose = (poseAt ?? PetAnimation.PoseAt)(state, local);
        var poseWeight = PetPixels.Weight(pose.Blend);
        var transition = _transitioning ? PetPixels.Weight(PetAnimation.Ease(local / PetAnimation.StateTransition)) : 256;
        var key = (state, pose.From, pose.To, poseWeight, transition);
        if (key == _last) return false;
        _last = key;

        PetPixels.Mix(frame(state, pose.From).Span, frame(state, pose.To).Span, _target, poseWeight);
        PetPixels.Mix(_previous, _target, _pixels, transition);
        if (transition == 256) _transitioning = false;
        return true;
    }
}
