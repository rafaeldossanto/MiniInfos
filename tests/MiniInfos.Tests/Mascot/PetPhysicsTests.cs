using System.Windows;
using System.Windows.Media;
using MiniInfos.Mascot;

namespace MiniInfos.Tests.Mascot;

public sealed class PetPhysicsTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void CircularCursorMotionCanDriveCompleteRevolutionsInEitherDirection(int direction)
    {
        var physics = Orbit(direction, 120, 4);
        Assert.True(physics.Angle * direction > 360, $"Only rotated {physics.Angle:0.0} degrees");
    }

    [Fact]
    public void StoppingTheCursorLeavesMomentumThatGravityAndFrictionSettle()
    {
        var physics = new PetPendulum();
        physics.Reset(new Point(0, 0), TimeSpan.Zero, 0.5);
        double peak = 0, atStop = 0;
        for (var frame = 1; frame <= 1200; frame++)
        {
            var seconds = frame / 120.0;
            var angle = physics.Advance(new Point(Math.Min(seconds, 0.12) * 500, 0), new Vector(0, 130), TimeSpan.FromSeconds(seconds));
            if (frame == 15) atStop = angle;
            if (frame > 15 && frame < 100) peak = Math.Max(peak, Math.Abs(angle - atStop));
        }
        Assert.True(peak > 5, "The owl stopped rotating as soon as the cursor stopped");
        Assert.InRange(Math.Abs(physics.Angle), 0, 0.5);
    }

    [Fact]
    public void OrdinarySwingsRemainConsistentAcrossFrameRates()
    {
        double Swing(int rate)
        {
            var physics = new PetPendulum();
            physics.Reset(new Point(0, 0), TimeSpan.Zero, 0.5);
            for (var frame = 1; frame <= rate * 3; frame++)
            {
                var seconds = frame / (double)rate;
                physics.Advance(new Point(12 * Math.Sin(seconds * 3), 0), new Vector(0, 130), TimeSpan.FromSeconds(seconds));
            }
            return physics.Angle;
        }
        Assert.InRange(Math.Abs(Swing(30) - Swing(120)), 0, 3);
    }

    [Fact]
    public void ARepeatedFrameDoesNotApplyTheSameImpulseAgain()
    {
        var physics = Orbit(1, 120, 2);
        var angle = physics.Angle;
        for (var read = 0; read < 100; read++) physics.Advance(new Point(123, 456), new Vector(0, 130), TimeSpan.FromSeconds(2));
        Assert.Equal(angle, physics.Angle);
        Assert.True(double.IsFinite(physics.Advance(new Point(100000, -100000), new Vector(0, 130), TimeSpan.FromSeconds(20))));
    }

    [Fact]
    public void ScreenScalingDoesNotChangeTheResponseToTheSameRelativeGesture()
    {
        var small = Orbit(1, 120, 2, 0.5);
        var scaled = Orbit(1, 120, 2, 1.5);
        Assert.Equal(small.Angle, scaled.Angle, 6);
    }

    [Fact]
    public void EveryRotationFitsTheWindowWhilePreservingItsGrip()
    {
        var anchor = new Point(45, 122);
        for (var degrees = -720; degrees <= 720; degrees += 15)
        {
            var matrix = Matrix.Identity;
            matrix.RotateAt(degrees, anchor.X, anchor.Y);
            var viewport = PetGeometry.Fit(matrix, 160, rotating: true);
            var shifted = viewport.Transform.Transform(anchor);
            Assert.InRange((shifted + (Vector)viewport.Bounds.TopLeft - anchor).Length, 0, 0.00001);
            foreach (var corner in new[] { new Point(0, 0), new Point(160, 0), new Point(0, 160), new Point(160, 160) })
            {
                var transformed = viewport.Transform.Transform(corner);
                Assert.InRange(transformed.X, -0.00001, viewport.Bounds.Width + 0.00001);
                Assert.InRange(transformed.Y, -0.00001, viewport.Bounds.Height + 0.00001);
            }
        }
    }

    [Fact]
    public void InteractiveViewportKeepsTheSameSizeThroughEveryRotation()
    {
        var expected = new Size(484, 484);
        foreach (var anchor in new[] { new Point(0, 0), new Point(160, 160), new Point(45, 122) })
        for (var angle = -720; angle <= 720; angle += 15)
        {
            var matrix = Matrix.Identity;
            matrix.ScaleAt(1.02, 1.02, anchor.X, anchor.Y);
            matrix.RotateAt(angle, anchor.X, anchor.Y);
            var viewport = PetGeometry.Around(matrix, 160, anchor);
            Assert.Equal(expected, viewport.Bounds.Size);
            Assert.InRange((viewport.Transform.Transform(anchor) - new Point(242, 242)).Length, 0, 0.00001);
            var image = new Rect(0, 0, 160, 160);
            image.Transform(viewport.Transform);
            Assert.True(new Rect(expected).Contains(image), $"Clipped at {angle} degrees");
        }
    }

    [Fact]
    public void ReturningFromHalfATurnDoesNotFlattenOrShrinkTheOwl()
    {
        var upsideDown = Matrix.Identity;
        upsideDown.RotateAt(180, 80, 100);
        var halfway = PetGeometry.Blend(upsideDown, Matrix.Identity, 0.5);
        Assert.Equal(1, halfway.Determinant, 6);
        Assert.Equal(1, halfway.Transform(new Vector(1, 0)).Length, 6);
        Assert.True(halfway.HasInverse);
    }

    private static PetPendulum Orbit(int direction, int rate, double seconds, double scale = 0.5)
    {
        var physics = new PetPendulum();
        var radius = 140 * scale;
        physics.Reset(new Point(direction * radius, 0), TimeSpan.Zero, scale);
        for (var frame = 1; frame <= rate * seconds; frame++)
        {
            var at = frame / (double)rate;
            var phase = at * Math.Tau / 0.6;
            physics.Advance(new Point(direction * radius * Math.Cos(phase), radius * Math.Sin(phase)),
                new Vector(0, 130), TimeSpan.FromSeconds(at));
        }
        return physics;
    }
}
