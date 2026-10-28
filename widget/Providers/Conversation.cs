namespace MiniInfos.Providers;

/// <summary>One chat or session and the tokens it spent, as the conversations card lists it.</summary>
/// <param name="ProviderId">The provider it belongs to: its icon and its filter.</param>
/// <param name="Key">Names it among the provider's conversations; never shown.</param>
/// <param name="Title">Its name: the one given in the app, else what best tells it apart.</param>
/// <param name="Project">The folder it worked in, by name, when that says more than the title.</param>
/// <param name="LastAt">When it last answered, in unix seconds: the list puts the latest first.</param>
/// <param name="ContextPercent">How full its context is, when its provider tells.</param>
public sealed record Conversation(
    string ProviderId,
    string Key,
    string Title,
    string? Project,
    long LastAt,
    TokenCount Tokens,
    double? ContextPercent = null);

/// <summary>The conversations card's list: every provider's conversations, the one that answered last first.</summary>
public static class ConversationList
{
    /// <summary>Enough to scroll back through a busy week; older ones still count in the totals.</summary>
    public const int Limit = 50;

    /// <param name="providerId">Only this provider's, or every provider's when null.</param>
    public static IReadOnlyList<Conversation> Recent(IEnumerable<IUsageProvider> providers, string? providerId = null) =>
        providers
            .Where(provider => providerId is null || provider.Id == providerId)
            .SelectMany(provider => provider.Conversations())
            .Where(conversation => conversation.Tokens.Total > 0)
            .OrderByDescending(conversation => conversation.LastAt)
            .ThenBy(conversation => conversation.Key, StringComparer.Ordinal)
            .Take(Limit)
            .ToArray();
}
