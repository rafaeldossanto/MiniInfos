using MiniInfos.Infrastructure.Windows;

namespace MiniInfos.Tests.Infrastructure.Windows;

public sealed class AssistantFollowerTests
{
    [Fact]
    public void TheCardFollowsOnlyChanges()
    {
        var isOpen = false;
        var shows = 0;
        var hides = 0;
        var follower = new AssistantFollower(() => isOpen, () => shows++, () => hides++);

        follower.Check();
        Assert.Equal((0, 0), (shows, hides));

        isOpen = true;
        follower.Check();
        follower.Check();
        Assert.Equal((1, 0), (shows, hides));

        isOpen = false;
        follower.Check();
        Assert.Equal((1, 1), (shows, hides));
    }

    [Fact]
    public void TheCardFollowsNothingWhileItIsTurnedOff()
    {
        var isOpen = true;
        var shows = 0;
        var hides = 0;
        var follower = new AssistantFollower(() => isOpen, () => shows++, () => hides++);
        follower.Check();

        follower.IsEnabled = false;
        isOpen = false;
        follower.Check();
        Assert.Equal((1, 0), (shows, hides));

        // Back on with every app closed: nothing hides at once, and the next opening shows the card.
        follower.IsEnabled = true;
        follower.Check();
        Assert.Equal((1, 0), (shows, hides));
        isOpen = true;
        follower.Check();
        Assert.Equal((2, 0), (shows, hides));
    }
}
