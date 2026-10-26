using System.Windows;

namespace MiniInfos.Mascot;

public enum PetBodyPart { Head, LeftEye, RightEye, LeftWing, RightWing, Body, Feet }
public readonly record struct PetHold(PetState State, Point Anchor, double Progress, double Angle);

/// <summary>Click, hold and drag gestures share one clock; they do not alter the observed token activity.</summary>
public sealed class PetInteraction
{
    public static readonly TimeSpan HoldDelay = TimeSpan.FromMilliseconds(220);
    public static readonly TimeSpan ProtectionFor = TimeSpan.FromMilliseconds(1450);
    private Point _pressedAt;
    private Point _pressScreen;
    private PetBodyPart _part;
    private TimeSpan _pressedTime;
    private TimeSpan _holdTime;
    private TimeSpan _lastMove;
    private TimeSpan _lastPhysics;
    private double _targetAngle;
    private double _angle;
    private PetState? _feedback;
    private TimeSpan _feedbackUntil;

    public bool IsPressed { get; private set; }
    public bool IsHeld { get; private set; }
    public Point PointerScreen { get; private set; }
    public int AnimationRevision { get; private set; }

    public static PetBodyPart PartAt(Point point)
    {
        if (InEllipse(point, new Point(130, 194), 22, 25)) return PetBodyPart.LeftEye;
        if (InEllipse(point, new Point(192, 194), 22, 25)) return PetBodyPart.RightEye;
        if (point.Y >= 278) return PetBodyPart.Feet;
        if (point.Y < 232 && InEllipse(point, new Point(160, 165), 85, 95)) return PetBodyPart.Head;
        if (point.X < 122) return PetBodyPart.LeftWing;
        if (point.X > 198) return PetBodyPart.RightWing;
        return PetBodyPart.Body;
    }

    public void Press(Point canvasPoint, Point screenPoint, TimeSpan now)
    {
        IsPressed = true; IsHeld = false;
        _pressedAt = canvasPoint;
        _pressScreen = PointerScreen = screenPoint;
        _part = PartAt(canvasPoint);
        _pressedTime = _lastMove = _lastPhysics = now;
        _targetAngle = _angle = 0;
    }

    public void Move(Point screenPoint, TimeSpan now)
    {
        if (!IsPressed) return;
        if ((screenPoint - PointerScreen).Length < 0.001) return;
        var seconds = Math.Max(0.008, (now - _lastMove).TotalSeconds);
        _targetAngle = Math.Clamp((screenPoint.X - PointerScreen.X) / seconds * 0.025, -13, 13);
        PointerScreen = screenPoint;
        _lastMove = now;
        if ((screenPoint - _pressScreen).Length >= 5) BeginHold(now);
    }

    public PetHold? HoldAt(TimeSpan now)
    {
        if (IsPressed && now - _pressedTime >= HoldDelay) BeginHold(now);
        if (!IsHeld) return null;
        var target = now - _lastMove < TimeSpan.FromMilliseconds(180) ? _targetAngle : 0;
        var dt = Math.Max(0, (now - _lastPhysics).TotalSeconds);
        _angle += (target - _angle) * (1 - Math.Exp(-dt / 0.11));
        _lastPhysics = now;
        var progress = PetAnimation.Ease((now - _holdTime) / PetAnimation.StateTransition);
        var state = HeldState(_part);
        var end = state switch
        {
            PetState.HeldHead or PetState.HeldBody => _pressedAt,
            PetState.HeldFeet => new Point(PetFrame.CanvasSize - _pressedAt.X, PetFrame.CanvasSize - _pressedAt.Y),
            _ => AnchorFor(state),
        };
        var anchor = _pressedAt + (end - _pressedAt) * progress;
        var sway = Math.Sin((now - _holdTime).TotalSeconds * 5) * 1.4;
        var gravity = state is PetState.HeldHead or PetState.HeldFeet
            ? Math.Clamp(Math.Atan2(160 - end.X, Math.Max(30, 230 - end.Y)) * 180 / Math.PI, -8, 8) : 0;
        return new(state, anchor, progress, Math.Clamp(gravity + _angle + sway, -14, 14));
    }

    public PetState? StateAt(TimeSpan now) => HoldAt(now)?.State ?? (now < _feedbackUntil ? _feedback : null);

    /// <returns>Whether this was a hold/drag, so the window can save the drop position.</returns>
    public bool Release(TimeSpan now)
    {
        if (!IsPressed) return false;
        HoldAt(now);
        var held = IsHeld;
        IsPressed = IsHeld = false;
        if (!held)
        {
            _feedback = _part is PetBodyPart.LeftEye or PetBodyPart.RightEye ? PetState.ProtectEyes : PetState.Active;
            _feedbackUntil = now + (_feedback == PetState.ProtectEyes ? ProtectionFor : TimeSpan.FromMilliseconds(700));
            AnimationRevision++;
        }
        else _feedback = null;
        return held;
    }

    public void CancelPress() => IsPressed = IsHeld = false;

    public static Point AnchorFor(PetState state) => state switch
    {
        PetState.HeldHead => new(160, 82),
        PetState.HeldLeftWing => new(85, 38),
        PetState.HeldRightWing => new(235, 38),
        PetState.HeldFeet => new(160, 24),
        _ => new(160, 225),
    };

    private void BeginHold(TimeSpan now)
    {
        if (IsHeld) return;
        IsHeld = true;
        _holdTime = _lastPhysics = now;
        AnimationRevision++;
    }

    private static PetState HeldState(PetBodyPart part) => part switch
    {
        PetBodyPart.Head or PetBodyPart.LeftEye or PetBodyPart.RightEye => PetState.HeldHead,
        PetBodyPart.LeftWing => PetState.HeldLeftWing,
        PetBodyPart.RightWing => PetState.HeldRightWing,
        PetBodyPart.Feet => PetState.HeldFeet,
        _ => PetState.HeldBody,
    };

    private static bool InEllipse(Point point, Point center, double xRadius, double yRadius) =>
        Math.Pow((point.X - center.X) / xRadius, 2) + Math.Pow((point.Y - center.Y) / yRadius, 2) <= 1;
}
