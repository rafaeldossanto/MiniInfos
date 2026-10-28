#if AVALONIA
using Avalonia;
#else
using System.Windows;
#endif

namespace MiniInfos.Mascot;

/// <summary>A rigid body pinned to the cursor. Gravity and cursor acceleration apply torque about the grip.</summary>
public sealed class PetPendulum
{
    private const double Gravity = 4200; // Canvas units/s²; independent of screen DPI and pet size.
    private const double Damping = 1.15;
    private const double GyrationRadius = 58;
    private const double StepSeconds = 1.0 / 240;
    private Point _pointer;
    private Vector _pointerVelocity;
    private Vector _acceleration;
    private TimeSpan _time;
    private double _screenScale;
    private double _angle;
    private double _angularVelocity;

    public double Angle => _angle * 180 / Math.PI;
    public double AngularVelocity => _angularVelocity * 180 / Math.PI;
    public Vector ScreenVelocity => _pointerVelocity * _screenScale;

    public void Reset(Point pointer, TimeSpan now, double screenScale)
    {
        _pointer = pointer;
        _pointerVelocity = default;
        _acceleration = default;
        _time = now;
        _screenScale = double.IsFinite(screenScale) && screenScale > 0 ? screenScale : 0.5;
        _angle = _angularVelocity = 0;
    }

    public double Advance(Point pointer, Vector massOffset, TimeSpan now)
    {
        var elapsed = (now - _time).TotalSeconds;
        if (elapsed <= 0 || !double.IsFinite(pointer.X) || !double.IsFinite(pointer.Y)) return Angle;
        var previousAcceleration = _acceleration;
        var acceleration = new Vector();
        if (elapsed <= 0.25)
        {
            var velocity = (pointer - _pointer) / (_screenScale * elapsed);
            var smoothed = _pointerVelocity + (velocity - _pointerVelocity) * (1 - Math.Exp(-elapsed / 0.035));
            acceleration = (smoothed - _pointerVelocity) / elapsed;
            if (acceleration.Length > 80000) acceleration *= 80000 / acceleration.Length;
            _acceleration += (acceleration - _acceleration) * (1 - Math.Exp(-elapsed / 0.022));
            _pointerVelocity = smoothed;
        }
        else
        {
            _pointerVelocity = _acceleration = previousAcceleration = default;
            // A suspended UI must not turn a cursor jump into an explosive impulse.
        }
        _time = now;
        _pointer = pointer;

        var duration = Math.Min(elapsed, 0.1);
        var steps = Math.Max(1, (int)Math.Ceiling(duration / StepSeconds));
        var dt = duration / steps;
        var inertia = (massOffset.X * massOffset.X + massOffset.Y * massOffset.Y) + GyrationRadius * GyrationRadius;
        for (var step = 0; step < steps; step++)
        {
            // Smooth the force itself and interpolate it within the frame, rather than changing torque in jumps.
            var input = previousAcceleration + (_acceleration - previousAcceleration) * ((step + 0.5) / steps);
            var force = new Vector(-input.X, Gravity - input.Y);
            var sin = Math.Sin(_angle);
            var cos = Math.Cos(_angle);
            var mass = new Vector(massOffset.X * cos - massOffset.Y * sin, massOffset.X * sin + massOffset.Y * cos);
            var torque = (mass.X * force.Y - mass.Y * force.X) / inertia;
            _angularVelocity = Math.Clamp((_angularVelocity + torque * dt) * Math.Exp(-Damping * dt), -Math.Tau * 6, Math.Tau * 6);
            _angle += _angularVelocity * dt; // Unwrapped: momentum can carry the owl through any number of revolutions.
        }
        return Angle;
    }
}
