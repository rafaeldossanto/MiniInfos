using MiniInfos.Providers;

namespace MiniInfos.Tests.Providers;

public sealed class TokenSummaryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 15, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void RecentHistoryIncludesEveryTokenKindAndKeepsEmptyIntervalsWithoutFutureOrOldUse()
    {
        LedgerEntry Entry(DateTimeOffset at, long amount) => new(at.ToUnixTimeSeconds(), amount, amount, amount, amount);
        var history = TokenSummary.Of([
            Entry(Now.AddHours(-6).AddSeconds(-1), 100), Entry(Now.AddHours(-6), 1),
            Entry(Now.AddHours(-6).AddMinutes(20).AddSeconds(-1), 2), Entry(Now.AddHours(-6).AddMinutes(20), 3),
            Entry(Now, 4), Entry(Now.AddSeconds(1), 200)
        ], Now, Now.AddHours(-5), Now.AddDays(-7)).History!;

        Assert.Equal(Now.AddHours(-6), history.Start);
        Assert.Equal(18, history.Tokens.Count);
        Assert.Equal((12L, 12L, 16L), (history.Tokens[0], history.Tokens[1], history.Tokens[^1]));
        Assert.All(history.Tokens.Skip(2).SkipLast(1), value => Assert.Equal(0, value));
        Assert.Equal(40, history.Tokens.Sum());
    }

    [Fact]
    public void HistoryAgesOutWithoutNewResponsesAndDoesNotChangeTheTotal()
    {
        LedgerEntry[] entries = [new(Now.ToUnixTimeSeconds(), 12, 3, 5, 2)];
        var first = TokenSummary.Of(entries, Now, Now.AddHours(-5), Now.AddDays(-7));
        var later = TokenSummary.Of(entries, Now.AddHours(6).AddSeconds(1), Now, Now);
        Assert.Equal(22, first.History!.Tokens[^1]);
        Assert.All(later.History!.Tokens, value => Assert.Equal(0, value));
        Assert.Equal(first.Total, later.Total);
    }

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
