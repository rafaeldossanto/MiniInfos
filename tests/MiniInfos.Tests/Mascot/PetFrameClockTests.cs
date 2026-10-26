using MiniInfos.Mascot;

namespace MiniInfos.Tests.Mascot;

public sealed class PetFrameClockTests
{
    [Theory]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(240)]
    public void InteractionUsesEveryDisplayFrame(int refreshRate)
    {
        var clock = new PetFrameClock();
        var painted = Enumerable.Range(0, refreshRate)
            .Count(frame => clock.Tick(TimeSpan.FromSeconds(frame / (double)refreshRate), interacting: true));
        Assert.Equal(refreshRate, painted);
    }

    [Fact]
    public void RestingAnimationKeepsItsLowerBudget()
    {
        var clock = new PetFrameClock();
        var painted = Enumerable.Range(0, 240)
            .Count(frame => clock.Tick(TimeSpan.FromSeconds(frame / 240.0), interacting: false));
        Assert.Equal(60, painted);
    }

    [Fact]
    public void DuplicateRenderingEventsDoNotRunPhysicsTwice()
    {
        var clock = new PetFrameClock();
        var time = TimeSpan.FromMilliseconds(10);
        Assert.True(clock.Tick(time, interacting: true));
        Assert.False(clock.Tick(time, interacting: true));
        Assert.True(clock.Tick(time + TimeSpan.FromMilliseconds(2), interacting: true));
    }
}
