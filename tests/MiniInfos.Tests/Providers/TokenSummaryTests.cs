using MiniInfos.Providers;

namespace MiniInfos.Tests.Providers;

public sealed class TokenSummaryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 15, 0, 0, TimeSpan.FromHours(-3));

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
}
