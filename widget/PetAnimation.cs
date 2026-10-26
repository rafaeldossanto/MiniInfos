using System.Runtime.InteropServices;

namespace MiniInfos;

public readonly record struct PetPose(int From, int To, double Blend);
public readonly record struct PetMotion(double X, double Y, double ScaleX, double ScaleY, double Angle);

/// <summary>Elapsed-time tracks: playback stays smooth even when the renderer misses a frame.</summary>
public static class PetAnimation
{
    public static readonly TimeSpan StateTransition = TimeSpan.FromMilliseconds(300);
    private static readonly int[] IdleDurations = [3500, 260, 240, 95, 55, 95, 360, 395];
    private static readonly int[] ActiveDurations = [130, 100, 90, 90, 110, 100, 110, 150];

    public static PetPose PoseAt(PetState state, TimeSpan elapsed)
    {
        var durations = state switch
        {
            PetState.Idle => IdleDurations,
            PetState.Active => ActiveDurations,
            _ => null,
        };
        var step = state == PetState.Sleep ? 450 : 260;
        var cycle = durations?.Sum() ?? step * PetSprites.FrameCount;
        var local = Math.Max(0, elapsed.TotalMilliseconds) % cycle;
        var index = 0;
        while (index < PetSprites.FrameCount - 1 && local >= (durations?[index] ?? step))
        {
            local -= durations?[index] ?? step;
            index++;
        }

        var duration = durations?[index] ?? step;
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
public sealed class PetAnimationPlayer(Func<PetState, int, ReadOnlyMemory<byte>> frame, int byteCount)
{
    private readonly byte[] _pixels = new byte[byteCount];
    private readonly byte[] _target = new byte[byteCount];
    private readonly byte[] _previous = new byte[byteCount];
    private PetState? _state;
    private TimeSpan _stateStarted;
    private (PetState State, int From, int To, int Pose, int Transition)? _last;
    private bool _transitioning;

    public ReadOnlyMemory<byte> Pixels => _pixels;
    internal byte[] Buffer => _pixels;

    public bool Draw(PetState state, TimeSpan elapsed)
    {
        if (_state != state)
        {
            _transitioning = _state is not null;
            if (_transitioning) _pixels.CopyTo(_previous, 0);
            _state = state;
            _stateStarted = elapsed;
            _last = null;
        }
        var local = elapsed - _stateStarted;
        var pose = PetAnimation.PoseAt(state, local);
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
