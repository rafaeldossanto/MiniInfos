using System.Windows;
using MiniInfos.Mascot;

namespace MiniInfos.Tests.Mascot;

public sealed class PetDropTests
{
    private static readonly Rect Bounds = new(-45, -65, 90, 115);

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(-90)]
    [InlineData(180)]
    public void AReleaseStartsAtTheActualPositionAndAngle(double angle)
    {
        var drop = Create(angle, 140);
        var first = drop.At(TimeSpan.Zero);
        Assert.Equal(new Vector(), first.Offset);
        Assert.Equal(angle, first.Angle);
        Assert.Equal(0.95, first.ScaleX);
        Assert.Equal(PetDropPhase.Falling, first.Phase);
        var falling = drop.At(TimeSpan.FromMilliseconds(70));
        Assert.True(falling.Offset.Y > 0);
        Assert.True(falling.Angle > first.Angle, "The release lost its angular momentum");
    }

    [Theory]
    [InlineData(0, PetLanding.Feet)]
    [InlineData(80, PetLanding.RightSide)]
    [InlineData(-80, PetLanding.LeftSide)]
    [InlineData(180, PetLanding.Head)]
    public void ContactDependsOnTheOrientationAtImpact(double angle, PetLanding expected)
    {
        var drop = Create(angle);
        Assert.Equal(expected, drop.Landing);
        Assert.Contains(Samples(drop), pose => pose.Phase == PetDropPhase.Impact && pose.ScaleY < 0.94);
        Assert.Contains(Samples(drop), pose => pose.Phase == PetDropPhase.Recovering);
        var final = drop.At(drop.Duration);
        Assert.True(final.IsSettled);
        Assert.InRange(Math.Abs(Math.IEEERemainder(final.Angle, 360)), 0, 0.0001);
        Assert.Equal(1, final.ScaleX, 6);
        Assert.Equal(1, final.ScaleY, 6);
        Assert.True(final.Offset.Y > 0);
    }

    [Fact]
    public void AHeadFirstLandingTakesLongerToRecoverThanLandingOnFeet()
    {
        Assert.True(Create(180).Duration > Create(0).Duration + TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public void ContactAndRecoveryDoNotTeleportBetweenPhases()
    {
        foreach (var angle in new[] { 0.0, 85, -95, 180 })
        {
            var drop = Create(angle, 75);
            var previous = drop.At(TimeSpan.Zero);
            for (var tick = 1; tick <= drop.Duration.TotalMilliseconds; tick++)
            {
                var current = drop.At(TimeSpan.FromMilliseconds(tick));
                Assert.InRange((current.Offset - previous.Offset).Length, 0, 2);
                Assert.InRange(Math.Abs(current.Angle - previous.Angle), 0, 2);
                previous = current;
            }
        }
    }

    [Fact]
    public void AShortDropKeepsItsLandingCloseToTheReleasePoint()
    {
        var drop = Create(40, 100, new Vector(2000, -1000));
        var pose = drop.At(drop.Duration);
        Assert.InRange(Math.Abs(pose.Offset.X), 0, 32);
        Assert.InRange(pose.Offset.Y, -20, 100);
        Assert.Equal(pose, drop.At(drop.Duration + TimeSpan.FromSeconds(20)));
    }

    [Fact]
    public void LandingAtTheScreenEdgeDoesNotAddAFallBelowTheAvailableSpace()
    {
        var drop = new PetDrop(0, 0, new(1, 1), new(0, 160), Bounds, Bounds, 160, 0);
        Assert.InRange(drop.At(TimeSpan.Zero).Offset.Length, 0, 0.001);
        Assert.InRange(drop.At(drop.Duration).Offset.Length, 0, 0.001);
    }

    [Fact]
    public void HalfTurnPreservesEveryPremultipliedPixelAndCanBeUndone()
    {
        var pixels = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        var original = pixels.ToArray();
        PetPixels.HalfTurn(pixels);
        Assert.Equal(new byte[] { 13, 14, 15, 16, 9, 10, 11, 12, 5, 6, 7, 8, 1, 2, 3, 4 }, pixels);
        PetPixels.HalfTurn(pixels);
        Assert.Equal(original, pixels);
    }

    private static PetDrop Create(double angle, double spin = 0, Vector velocity = default) =>
        new(angle, spin, new(0.95, 0.95), velocity, Bounds, Bounds, 160, 48);

    private static IEnumerable<PetDropPose> Samples(PetDrop drop)
    {
        for (var ms = 0; ms <= drop.Duration.TotalMilliseconds; ms += 10) yield return drop.At(TimeSpan.FromMilliseconds(ms));
    }
}
