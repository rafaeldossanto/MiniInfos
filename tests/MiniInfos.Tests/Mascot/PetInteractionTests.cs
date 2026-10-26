using System.Windows;
using MiniInfos.Mascot;

namespace MiniInfos.Tests.Mascot;

public sealed class PetInteractionTests
{
    [Theory]
    [InlineData(130, 194, PetBodyPart.LeftEye)]
    [InlineData(192, 194, PetBodyPart.RightEye)]
    [InlineData(160, 100, PetBodyPart.Head)]
    [InlineData(90, 245, PetBodyPart.LeftWing)]
    [InlineData(230, 245, PetBodyPart.RightWing)]
    [InlineData(160, 250, PetBodyPart.Body)]
    [InlineData(144, 292, PetBodyPart.Feet)]
    public void ClickRegionsFollowTheOwlsFeatures(double x, double y, PetBodyPart expected) =>
        Assert.Equal(expected, PetInteraction.PartAt(new Point(x, y)));

    [Theory]
    [InlineData(130)]
    [InlineData(192)]
    public void AQuickEyePokeProtectsTheFaceAndThenReturnsToUsage(double x)
    {
        var interaction = new PetInteraction();
        interaction.Press(new Point(x, 194), new Point(500, 500), TimeSpan.Zero);
        interaction.Move(new Point(502, 501), TimeSpan.FromMilliseconds(30));
        Assert.False(interaction.Release(TimeSpan.FromMilliseconds(100)));
        Assert.Equal(PetState.ProtectEyes, interaction.StateAt(TimeSpan.FromMilliseconds(300)));
        Assert.Null(interaction.StateAt(TimeSpan.FromMilliseconds(100) + PetInteraction.ProtectionFor));
    }

    [Theory]
    [InlineData(160, 100, PetState.HeldHead)]
    [InlineData(90, 245, PetState.HeldLeftWing)]
    [InlineData(230, 245, PetState.HeldRightWing)]
    [InlineData(144, 292, PetState.HeldFeet)]
    [InlineData(160, 250, PetState.HeldBody)]
    public void ADragSelectsAPoseFromThePartThatWasGrabbed(double x, double y, PetState expected)
    {
        var interaction = new PetInteraction();
        interaction.Press(new Point(x, y), new Point(500, 500), TimeSpan.Zero);
        interaction.Move(new Point(525, 505), TimeSpan.FromMilliseconds(30));
        Assert.Equal(expected, interaction.StateAt(TimeSpan.FromMilliseconds(40)));
        Assert.True(interaction.Release(TimeSpan.FromMilliseconds(400)));
        Assert.Null(interaction.StateAt(TimeSpan.FromMilliseconds(401)));
    }

    [Fact]
    public void HoldingAnEyeGrabsTheHeadInsteadOfPokingIt()
    {
        var interaction = new PetInteraction();
        interaction.Press(new Point(130, 194), new Point(500, 500), TimeSpan.Zero);
        Assert.Null(interaction.StateAt(PetInteraction.HoldDelay - TimeSpan.FromMilliseconds(1)));
        Assert.Equal(PetState.HeldHead, interaction.StateAt(PetInteraction.HoldDelay));
        Assert.True(interaction.Release(TimeSpan.FromSeconds(1)));
        Assert.Null(interaction.StateAt(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void TheGripMovesSmoothlyToTheRaisedWingTip()
    {
        var interaction = new PetInteraction();
        var pressed = new Point(90, 245);
        interaction.Press(pressed, new Point(500, 500), TimeSpan.Zero);
        interaction.Move(new Point(525, 500), TimeSpan.FromMilliseconds(10));
        Assert.Equal(pressed, interaction.HoldAt(TimeSpan.FromMilliseconds(10))!.Value.Anchor);
        var middle = interaction.HoldAt(TimeSpan.FromMilliseconds(160))!.Value;
        Assert.InRange(middle.Anchor.Y, 39, 244);
        Assert.Equal(PetInteraction.AnchorFor(PetState.HeldLeftWing),
            interaction.HoldAt(TimeSpan.FromMilliseconds(310))!.Value.Anchor);
    }

    [Fact]
    public void GrabbingOneFootKeepsThatFootAtTheCursorWhenUpsideDown()
    {
        var interaction = new PetInteraction();
        interaction.Press(new Point(144, 292), new Point(500, 500), TimeSpan.Zero);
        interaction.Move(new Point(525, 500), TimeSpan.FromMilliseconds(10));
        Assert.Equal(new Point(176, 28), interaction.HoldAt(TimeSpan.FromSeconds(1))!.Value.Anchor);
    }

    [Fact]
    public void LosingCaptureDoesNotCreateAFakeEyePoke()
    {
        var interaction = new PetInteraction();
        interaction.Press(new Point(130, 194), new Point(500, 500), TimeSpan.Zero);
        interaction.CancelPress();
        Assert.False(interaction.IsPressed);
        Assert.False(interaction.Release(TimeSpan.FromMilliseconds(100)));
        Assert.Null(interaction.StateAt(TimeSpan.FromMilliseconds(100)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReleaseAndCaptureLossPreserveMotionForTheDropButANewGripStartsFresh(bool lostCapture)
    {
        var interaction = new PetInteraction();
        interaction.Press(new Point(160, 100), new Point(500, 500), TimeSpan.Zero);
        for (var ms = 10; ms <= 100; ms += 10)
        {
            interaction.Move(new Point(500 + ms, 500), TimeSpan.FromMilliseconds(ms));
            interaction.HoldAt(TimeSpan.FromMilliseconds(ms));
        }
        var velocity = interaction.ReleaseVelocity;
        var spin = interaction.AngularVelocity;
        Assert.True(velocity.Length > 1 && Math.Abs(spin) > 1);
        if (lostCapture) interaction.CancelPress();
        else interaction.Release(TimeSpan.FromMilliseconds(100));
        Assert.Equal(velocity, interaction.ReleaseVelocity);
        Assert.Equal(spin, interaction.AngularVelocity);

        interaction.Press(new Point(160, 100), new Point(600, 500), TimeSpan.FromMilliseconds(200));
        Assert.Equal(new Vector(), interaction.ReleaseVelocity);
        Assert.Equal(0, interaction.AngularVelocity);
    }

    [Fact]
    public void PokingAgainReplaysTheReactionInsteadOfSnappingToTheFirstFrame()
    {
        var interaction = new PetInteraction();
        interaction.Press(new Point(130, 194), new Point(500, 500), TimeSpan.Zero);
        interaction.Release(TimeSpan.FromMilliseconds(50));
        var first = interaction.AnimationRevision;
        interaction.Press(new Point(192, 194), new Point(500, 500), TimeSpan.FromMilliseconds(400));
        interaction.Release(TimeSpan.FromMilliseconds(450));
        Assert.True(interaction.AnimationRevision > first);
        Assert.Equal(PetState.ProtectEyes, interaction.StateAt(TimeSpan.FromMilliseconds(1600)));
    }

    [Fact]
    public void TheOwlTurnsUprightAroundHerVisibleCenterNotTheMassCenterHerSwingUses()
    {
        Assert.Equal(new Point(160, 200), PetAppearances.Athena.RestCenter);
        Assert.Equal(new Point(160, 230), PetAppearances.Athena.Anatomy.MassCenter.Point);
    }
}
