namespace MiniInfos.Providers;

public readonly record struct TokenCount(long Input, long Output, long CacheRead, long CacheWrite)
{
    public long Total => Input + Output + CacheRead + CacheWrite;

    public static TokenCount operator +(TokenCount left, TokenCount right) => new(
        left.Input + right.Input,
        left.Output + right.Output,
        left.CacheRead + right.CacheRead,
        left.CacheWrite + right.CacheWrite);
}

/// <summary>One counted use of a model: when, in unix seconds, and its tokens.</summary>
public sealed record LedgerEntry(long At, long Input, long Output, long CacheRead, long CacheWrite)
{
    public TokenCount Count => new(Input, Output, CacheRead, CacheWrite);
}

/// <summary>A ledger's tokens: today, inside each of the plan's two windows, and in all.</summary>
public sealed record TokenSummary(TokenCount Today, TokenCount ShortWindow, TokenCount LongWindow, TokenCount Total)
{
    /// <param name="now">Local now: today counts from its midnight.</param>
    public static TokenSummary Of(
        IEnumerable<LedgerEntry> entries,
        DateTimeOffset now,
        DateTimeOffset shortWindowStart,
        DateTimeOffset longWindowStart)
    {
        var today = new DateTimeOffset(now.Date, now.Offset).ToUnixTimeSeconds();
        var shortStart = shortWindowStart.ToUnixTimeSeconds();
        var longStart = longWindowStart.ToUnixTimeSeconds();
        TokenCount todayCount = default, shortCount = default, longCount = default, total = default;

        foreach (var entry in entries)
        {
            var count = entry.Count;
            total += count;
            if (entry.At >= today) todayCount += count;
            if (entry.At >= shortStart) shortCount += count;
            if (entry.At >= longStart) longCount += count;
        }

        return new TokenSummary(todayCount, shortCount, longCount, total);
    }
}
