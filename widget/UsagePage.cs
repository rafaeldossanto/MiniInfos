namespace MiniInfos;

/// <summary>A plan's rate-limit window: how long it runs, and the account's reading of it when there is one.</summary>
public sealed record PlanWindow(TimeSpan Length, RateLimit? Limit)
{
    /// <summary>Where its tokens start counting: the window's own start when it has a reading, else the last <see cref="Length"/>.</summary>
    public DateTimeOffset Start(DateTimeOffset now) =>
        Limit?.ResetsAtTime is { } reset
            ? (reset > now ? reset - Length : reset)
            : now - Length;
}

/// <summary>One window as a page draws it: the plan's reading and the tokens counted inside it.</summary>
public sealed record WindowLine(PlanWindow Window, TokenCount Tokens);

/// <summary>
/// One provider's numbers, ready to draw. Every page of the card is drawn the same way from one,
/// so the card knows no provider by name.
/// </summary>
/// <param name="TokensOrigin">Where the tokens come from, said in the count's tooltips.</param>
/// <param name="Activity">The bottom line: the chat or session that was active last.</param>
/// <param name="ActivityHint">What that line is, said in its tooltip.</param>
/// <param name="Updated">The header's note on how fresh the limits are.</param>
/// <param name="RefreshHint">What ↻ does for this provider, said in its tooltip.</param>
public sealed record UsagePage(
    string Title,
    WindowLine ShortWindow,
    WindowLine LongWindow,
    TokenCount Today,
    TokenCount Total,
    string TokensOrigin,
    string Activity,
    string ActivityHint,
    string Updated,
    string RefreshHint);

/// <summary>A source of usage numbers with a page of its own on the card: Claude, Codex, or the next one.</summary>
public interface IUsageProvider
{
    /// <summary>Saved with the card's settings so it reopens on the same page; never shown.</summary>
    string Id { get; }

    /// <summary>Whether its app or one of its chats is open now: the card shows when one opens.</summary>
    bool IsInUse();

    /// <summary>Reads the latest numbers; every provider reads on each refresh so its ledger keeps up with its logs.</summary>
    /// <param name="isShown">Its page is on screen: reads only a visible page needs (starting a process, asking a server) may run.</param>
    /// <param name="force">The person asked for fresh numbers (↻): wait for those reads instead of using the last ones.</param>
    Task<UsagePage> ReadAsync(DateTimeOffset now, bool isShown, bool force);

    /// <summary>Writes what it keeps before the app quits.</summary>
    void Flush();
}

public static class UsageProviders
{
    /// <summary>Every provider, in the order of the card's pages: a new one is one more entry here.</summary>
    public static IReadOnlyList<IUsageProvider> Create() => [new ClaudeUsage(), new CodexUsage()];
}
