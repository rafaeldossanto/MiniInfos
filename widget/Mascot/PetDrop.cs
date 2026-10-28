#if AVALONIA
using Avalonia;
#else
using System.Windows;
#endif

namespace MiniInfos.Mascot;

public enum PetDropPhase { Falling, Impact, Recovering, Settled }
public enum PetLanding { Feet, LeftSide, RightSide, Head }
public readonly record struct PetDropPose(Vector Offset, double Angle, double ScaleX, double ScaleY,
    PetDropPhase Phase, PetLanding Landing)
{
    public bool IsSettled => Phase == PetDropPhase.Settled;
}

/// <summary>A short local fall from the actual release pose, followed by contact and orientation-specific recovery.</summary>
public sealed class PetDrop
{
    private readonly double _angle;
    private readonly double _spin;
    private readonly Vector _scale;
    private readonly Vector _velocity;
    private readonly Rect _sourceBounds;
    private readonly Rect _restBounds;
    private readonly double _gravity;
    private readonly double _floor;
    private readonly double _flight;
    private readonly double _impactAngle;
    private readonly double _impactSpin;
    private readonly double _contact;
    private readonly double _recovery;
    private readonly double _recoveryAngle;
    private readonly double _targetAngle;
    private readonly double _bounce;
    public PetLanding Landing { get; }
    public TimeSpan Duration => TimeSpan.FromTicks((long)Math.Ceiling((_flight + _contact + _recovery) * TimeSpan.TicksPerSecond));

    public PetDrop(double angle, double spin, Vector scale, Vector velocity, Rect sourceBounds, Rect restBounds,
        double size, double distance)
    {
        _angle = angle;
        _spin = Math.Clamp(spin, -900, 900);
        _scale = scale;
        _velocity = new(Math.Clamp(velocity.X * 0.3, -size, size), Math.Clamp(velocity.Y * 0.3, -size, size));
        _sourceBounds = sourceBounds;
        _restBounds = restBounds;
        _gravity = 1400 * size / 160;
        _floor = Bottom(sourceBounds, angle, scale.X, scale.Y) + Math.Max(0, distance);

        // Solve the first contact against the rotated silhouette; the result is independent of frame cadence.
        var previous = 0.0;
        _flight = 1.5;
        for (var time = 1.0 / 240; time <= 1.5; time += 1.0 / 240)
        {
            if (FlightY(time) + Bottom(sourceBounds, FlightAngle(time), scale.X, scale.Y) >= _floor)
            {
                var low = previous;
                var high = time;
                for (var iteration = 0; iteration < 24; iteration++)
                {
                    var mid = (low + high) / 2;
                    if (FlightY(mid) + Bottom(sourceBounds, FlightAngle(mid), scale.X, scale.Y) < _floor) low = mid;
                    else high = mid;
                }
                _flight = high;
                break;
            }
            previous = time;
        }
        _impactAngle = FlightAngle(_flight);
        _impactSpin = _spin * Math.Exp(-1.6 * _flight);
        var tilt = Math.IEEERemainder(_impactAngle, 360);
        Landing = Math.Abs(tilt) <= 38 ? PetLanding.Feet : Math.Abs(tilt) >= 135 ? PetLanding.Head
            : tilt < 0 ? PetLanding.LeftSide : PetLanding.RightSide;
        (_contact, _recovery) = Landing switch
        {
            PetLanding.Feet => (0.14, 0.28), PetLanding.Head => (0.23, 0.57), _ => (0.19, 0.43),
        };
        _recoveryAngle = ContactAngle(_contact);
        _targetAngle = _recoveryAngle - Math.IEEERemainder(_recoveryAngle, 360);
        if (Landing == PetLanding.Head && Math.Abs(_impactSpin) > 60)
            _targetAngle = (_impactSpin > 0 ? Math.Ceiling(_recoveryAngle / 360) : Math.Floor(_recoveryAngle / 360)) * 360;
        _bounce = size * (Landing == PetLanding.Feet ? 0.035 : 0.06);
    }

    public PetDropPose At(TimeSpan elapsed)
    {
        var time = Math.Max(0, elapsed.TotalSeconds);
        if (time < _flight)
            return new(new(X(time), FlightY(time)), FlightAngle(time), _scale.X, _scale.Y, PetDropPhase.Falling, Landing);
        var contactTime = time - _flight;
        var x = X(Math.Min(time, _flight + _contact));
        if (contactTime < _contact)
        {
            var progress = contactTime / _contact;
            var pulse = Math.Sin(Math.PI * progress);
            var strength = Landing == PetLanding.Feet ? 0.12 : Landing == PetLanding.Head ? 0.24 : 0.18;
            var sx = _scale.X * (1 + strength * 0.6 * pulse);
            var sy = _scale.Y * (1 - strength * pulse);
            var angle = ContactAngle(contactTime);
            return new(new(x, _floor - Bottom(_sourceBounds, angle, sx, sy) - _bounce * pulse), angle, sx, sy, PetDropPhase.Impact, Landing);
        }
        var u = Math.Clamp((contactTime - _contact) / _recovery, 0, 1);
        var ease = PetAnimation.Ease(u);
        var scaleX = _scale.X + (1 - _scale.X) * ease;
        var scaleY = _scale.Y + (1 - _scale.Y) * ease;
        // The contact's remaining spin joins the roll/get-up motion without snapping back to upright in midair.
        var tangent = _impactSpin * Math.Exp(-14 * _contact) * _recovery;
        var angleAt = (2 * u * u * u - 3 * u * u + 1) * _recoveryAngle
            + (u * u * u - 2 * u * u + u) * tangent + (-2 * u * u * u + 3 * u * u) * _targetAngle;
        var bounds = BlendBounds(_sourceBounds, _restBounds, ease);
        return new(new(x, _floor - Bottom(bounds, angleAt, scaleX, scaleY)), angleAt, scaleX, scaleY,
            u >= 1 ? PetDropPhase.Settled : PetDropPhase.Recovering, Landing);
    }

    private double FlightY(double time) => _velocity.Y * time + _gravity * time * time / 2;
    private double FlightAngle(double time) => _angle + _spin * (1 - Math.Exp(-1.6 * time)) / 1.6;
    private double ContactAngle(double time) => _impactAngle + _impactSpin * (1 - Math.Exp(-14 * time)) / 14;
    private double X(double time) => _velocity.X * (1 - Math.Exp(-5 * time)) / 5;

    private static double Bottom(Rect bounds, double angle, double scaleX, double scaleY)
    {
        var radians = angle * Math.PI / 180;
        var x = Math.Sin(radians) * scaleX;
        var y = Math.Cos(radians) * scaleY;
        return Math.Max(bounds.Left * x, bounds.Right * x) + Math.Max(bounds.Top * y, bounds.Bottom * y);
    }

    private static Rect BlendBounds(Rect from, Rect to, double progress) => new(
        from.X + (to.X - from.X) * progress, from.Y + (to.Y - from.Y) * progress,
        from.Width + (to.Width - from.Width) * progress, from.Height + (to.Height - from.Height) * progress);
}
