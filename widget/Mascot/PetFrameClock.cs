namespace MiniInfos.Mascot;

/// <summary>Interactive motion follows each display frame; resting animation keeps its lighter 60 Hz budget.</summary>
public sealed class PetFrameClock
{
    private TimeSpan? _lastRenderingTime;
    private long _restingFrame = -1;

    public bool Tick(TimeSpan renderingTime, bool interacting)
    {
        if (_lastRenderingTime == renderingTime) return false;
        var restingFrame = (long)(renderingTime.TotalSeconds * 60);
        if (!interacting && restingFrame == _restingFrame) return false;
        _lastRenderingTime = renderingTime;
        _restingFrame = restingFrame;
        return true;
    }
}
