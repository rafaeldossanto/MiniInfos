using System.Globalization;

namespace MiniInfos.Tests;

/// <summary>What the card computes and writes, apart from drawing it.</summary>
public sealed class CardModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 15, 0, 0, TimeSpan.FromHours(-3));

    [Theory]
    [InlineData(999, "999")]
    [InlineData(51_200, "51,2k")]
    [InlineData(1_052_000, "1,1M")]
    [InlineData(3_747_374_621, "3,7B")]
    public void ShortCounts(long value, string text) => Assert.Equal(text, Format.Count(value));

    [Fact]
    public void WholeCountsGroupEveryThreeDigits() => Assert.Equal("1.412.345.678", Format.Full(1_412_345_678));

    [Theory]
    [InlineData(300, "Janela 5h", "últimas 5h")]
    [InlineData(10_080, "Semanal", "últimos 7 dias")]
    [InlineData(45, "Janela 45min", "últimos 45min")]
    [InlineData(1_440, "Janela 24h", "últimas 24h")]
    public void WindowsAreNamedByTheirLength(int minutes, string label, string span)
    {
        Assert.Equal(label, Format.WindowLabel(TimeSpan.FromMinutes(minutes)));
        Assert.Equal(span, Format.WindowSpan(TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void ResetsReadAsTheTimeLeft()
    {
        Assert.Equal("reinicia em 2h 13min", Format.Reset(Now.AddMinutes(133), Now));
        Assert.Equal("reinicia em 3d 4h", Format.Reset(Now.AddDays(3).AddHours(4), Now));
        Assert.Equal("reiniciou", Format.Reset(Now.AddMinutes(-1), Now));
    }

    [Fact]
    public void AWindowWithAReadingCountsFromItsOwnStart()
    {
        var reset = Now.AddHours(2);
        var window = new PlanWindow(TimeSpan.FromHours(5), Limit(reset));

        Assert.Equal(reset.AddHours(-5), window.Start(Now));
    }

    [Fact]
    public void AWindowThatResetAlreadyCountsFromTheReset()
    {
        var reset = Now.AddMinutes(-10);

        Assert.Equal(reset, new PlanWindow(TimeSpan.FromHours(5), Limit(reset)).Start(Now));
    }

    [Fact]
    public void AWindowWithNoReadingCountsBackItsLength() =>
        Assert.Equal(Now.AddDays(-7), new PlanWindow(TimeSpan.FromDays(7), null).Start(Now));

    [Fact]
    public void ASummarySplitsTodayTheWindowsAndTheTotal()
    {
        LedgerEntry Entry(DateTimeOffset at, long tokens) => new(at.ToUnixTimeSeconds(), tokens, 0, 0, 0);
        var entries = new[]
        {
            Entry(Now.AddMinutes(-30), 1),
            Entry(Now.AddHours(-6), 10),
            Entry(Now.AddDays(-2), 100),
            Entry(Now.AddDays(-30), 1_000),
        };

        var summary = TokenSummary.Of(entries, Now, Now.AddHours(-5), Now.AddDays(-7));

        Assert.Equal(11, summary.Today.Total);
        Assert.Equal(1, summary.ShortWindow.Total);
        Assert.Equal(111, summary.LongWindow.Total);
        Assert.Equal(1_111, summary.Total.Total);
    }

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
    public void DraggingAnEdgeKeepsTheCardsProportions()
    {
        const double aspect = 264.0 / 228.0;

        var right = AspectSizing.Fit(new PixelRect(100, 100, 500, 200), AspectSizing.RightEdge, aspect, 198, 792);
        Assert.Equal((400, (int)Math.Round(400 / aspect)), (right.Width, right.Height));

        var corner = AspectSizing.Fit(new PixelRect(50, 50, 364, 328), AspectSizing.TopLeftCorner, aspect, 198, 792);
        Assert.Equal((364, 328), (corner.Right, corner.Bottom));

        Assert.Equal(792, AspectSizing.Fit(new PixelRect(0, 0, 2_000, 10), AspectSizing.RightEdge, aspect, 198, 792).Width);
        Assert.Equal(198, AspectSizing.Fit(new PixelRect(300, 0, 364, 10), AspectSizing.LeftEdge, aspect, 198, 792).Width);
    }

    private static RateLimit Limit(DateTimeOffset reset) => new("primary", 50, reset.ToString("O", CultureInfo.InvariantCulture));
}
