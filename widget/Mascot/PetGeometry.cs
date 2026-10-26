using System.Windows;
using System.Windows.Media;

namespace MiniInfos.Mascot;

public readonly record struct PetViewport(Matrix Transform, Rect Bounds);

/// <summary>Pose interpolation and a transparent viewport that contains a rotating sprite.</summary>
public static class PetGeometry
{
    public static Matrix Blend(Matrix from, Matrix to, double progress, double? unwrappedAngle = null)
    {
        if (progress <= 0) return from;
        if (progress >= 1) return to;
        double Lerp(double a, double b) => a + (b - a) * progress;
        static double ScaleX(Matrix m) => Math.Sqrt(m.M11 * m.M11 + m.M12 * m.M12);
        var firstScale = ScaleX(from);
        var lastScale = ScaleX(to);
        var firstAngle = Math.Atan2(from.M12, from.M11);
        var angle = unwrappedAngle is { } degrees ? degrees * Math.PI / 180
            : firstAngle + Math.IEEERemainder(Math.Atan2(to.M12, to.M11) - firstAngle, Math.Tau);
        var result = Matrix.Identity;
        result.Scale(Lerp(firstScale, lastScale), Lerp(from.Determinant / firstScale, to.Determinant / lastScale));
        result.Rotate(Lerp(firstAngle, angle) * 180 / Math.PI);
        result.Translate(Lerp(from.OffsetX, to.OffsetX), Lerp(from.OffsetY, to.OffsetY));
        return result;
    }

    public static PetViewport Fit(Matrix transform, double size, bool rotating)
    {
        var bounds = new Rect(0, 0, size, size);
        if (rotating)
        {
            var rotated = bounds;
            rotated.Transform(transform);
            bounds.Union(rotated);
        }
        transform.Translate(-bounds.X, -bounds.Y);
        return new(transform, bounds);
    }

    /// <summary>A constant canvas avoids reallocating the layered window's render surface at every rotation.</summary>
    public static PetViewport Around(Matrix transform, double size, Point anchor)
    {
        // The farthest canvas corner is sqrt(2) * size from any grip, with room for the pose's small stretch.
        var extent = Math.Ceiling(size * 3) + 4;
        var pivot = transform.Transform(anchor);
        var bounds = new Rect(pivot.X - extent / 2, pivot.Y - extent / 2, extent, extent);
        transform.Translate(-bounds.X, -bounds.Y);
        return new(transform, bounds);
    }
}
