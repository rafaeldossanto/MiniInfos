namespace MiniInfos.Providers;

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
    string RefreshHint,
    IReadOnlyList<UsageQuotaGroup>? QuotaGroups = null);

/// <summary>Models sharing account limits. Null on a page keeps the standard two-window presentation.</summary>
public sealed record UsageQuotaGroup(string Title, string? Models, IReadOnlyList<PlanWindow> Windows);
