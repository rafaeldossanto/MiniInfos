using MiniInfos.Mascot;

namespace MiniInfos.Tests.Mascot;

public sealed class PetAnimationTests
{
    [Fact]
    public void InterpolationKeepsAnOpaqueOwlOpaque()
    {
        byte[] from = [20, 40, 60, 255];
        byte[] to = [60, 80, 100, 255];
        var output = new byte[4];
        PetPixels.Mix(from, to, output, 128);
        Assert.Equal(new byte[] { 40, 60, 80, 255 }, output);
    }

    [Fact]
    public void ATransparentWingEdgeKeepsItsPremultipliedAlpha()
    {
        var output = new byte[4];
        PetPixels.Mix(new byte[4], new byte[] { 40, 80, 120, 160 }, output, 64);
        Assert.Equal(new byte[] { 10, 20, 30, 40 }, output);
        Assert.All(output.Take(3), channel => Assert.True(channel <= output[3]));
    }

    [Fact]
    public void ChangingStateStartsFromTheDisplayedPoseIncludingWhenInterrupted()
    {
        byte[] idle = [0, 0, 200, 255], active = [200, 0, 0, 255], alert = [0, 200, 0, 255];
        var player = new PetAnimationPlayer((state, _) => state switch
        {
            PetState.Idle => idle,
            PetState.Active => active,
            _ => alert,
        }, 4);
        player.Draw(PetState.Idle, TimeSpan.Zero);
        player.Draw(PetState.Active, TimeSpan.FromSeconds(1));
        Assert.Equal(idle, player.Pixels.ToArray());
        player.Draw(PetState.Active, TimeSpan.FromMilliseconds(1150));
        Assert.Equal(new byte[] { 100, 0, 100, 255 }, player.Pixels.ToArray());
        var halfway = player.Pixels.ToArray();
        player.Draw(PetState.Alert, TimeSpan.FromMilliseconds(1150));
        Assert.Equal(halfway, player.Pixels.ToArray());
        player.Draw(PetState.Alert, TimeSpan.FromMilliseconds(1450));
        Assert.Equal(alert, player.Pixels.ToArray());
    }

    [Fact]
    public void DroppedFramesDoNotSlowDownPlayback()
    {
        var frames = Enumerable.Range(0, PetSprites.FrameCount).Select(index => new byte[] { (byte)(index * 20), 40, 100, 255 }).ToArray();
        var normal = new PetAnimationPlayer((_, index) => frames[index], 4);
        var delayed = new PetAnimationPlayer((_, index) => frames[index], 4);
        normal.Draw(PetState.Active, TimeSpan.Zero);
        delayed.Draw(PetState.Active, TimeSpan.Zero);
        for (var time = 10; time <= 730; time += 10) normal.Draw(PetState.Active, TimeSpan.FromMilliseconds(time));
        delayed.Draw(PetState.Active, TimeSpan.FromMilliseconds(730));
        Assert.Equal(normal.Pixels.ToArray(), delayed.Pixels.ToArray());
    }

    [Fact]
    public void ReplayingTheSameInteractionStartsAtThePixelsAlreadyOnScreen()
    {
        var frames = Enumerable.Range(0, PetSprites.FrameCount).Select(index => new byte[] { (byte)(index * 20), 40, 100, 255 }).ToArray();
        var player = new PetAnimationPlayer((_, index) => frames[index], 4);
        player.Draw(PetState.ProtectEyes, TimeSpan.Zero, 1);
        player.Draw(PetState.ProtectEyes, TimeSpan.FromMilliseconds(650), 1);
        var before = player.Pixels.ToArray();
        player.Draw(PetState.ProtectEyes, TimeSpan.FromMilliseconds(650), 2);
        Assert.Equal(before, player.Pixels.ToArray());
    }

    [Fact]
    public void RestingBreathMovesBetweenSpriteKeysAndRemainsSubtle()
    {
        var first = PetAnimation.MotionAt(TimeSpan.Zero);
        var later = PetAnimation.MotionAt(TimeSpan.FromMilliseconds(200));
        Assert.NotEqual(first, later);
        for (var time = 0; time <= 10_000; time += 17)
        {
            var motion = PetAnimation.MotionAt(TimeSpan.FromMilliseconds(time));
            Assert.InRange(Math.Abs(motion.Y), 0, 0.0061);
            Assert.InRange(Math.Abs(motion.Angle), 0, 0.66);
            Assert.InRange(motion.ScaleY, 0.985, 1.015);
        }
    }
}
