namespace MiniInfos.Providers;

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
public sealed record WindowLine(PlanWindow Window, TokenCount Tokens, string? Label = null);
