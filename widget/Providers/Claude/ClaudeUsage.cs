using MiniInfos.Providers;
using MiniInfos.Sources;
using MiniInfos.UI.Formatting;

namespace MiniInfos.Providers.Claude;

/// <summary>
/// Claude's page: the plan's limits as the miniinfos mod publishes them from each chat, and the
/// tokens of every Claude Code chat from its transcripts.
/// </summary>
public sealed class ClaudeUsage : IUsageProvider
{
    private const string FiveHourKind = "five_hour";
    private const string WeekKind = "seven_day";

    private static readonly TimeSpan FiveHours = TimeSpan.FromHours(5);
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);

    private readonly ClaudeUsageLedger _ledger;
    private readonly TitleCache _titles = new();
    private FeedState? _lastFeed;

    public ClaudeUsage(ClaudeUsageLedger? ledger = null) => _ledger = ledger ?? ClaudeUsageLedger.Load();

    public string Id => "claude";

    public bool IsInstalled() => ClaudeInstall.IsPresent();

    /// <summary>The desktop app's window, or any Claude Code chat still running: in a terminal, an editor or the app.</summary>
    public bool IsInUse() => ClaudeApp.IsOpen() || _lastFeed?.HasOpenChat == true;

    public async Task<UsagePage> ReadAsync(DateTimeOffset now, bool isShown, bool force)
    {
        if (force) await UsageSources.Shared.RefreshAsync();
        var feed = await Task.Run(() => FeedReader.Read(now));
        _lastFeed = feed;
        var fiveHour = new PlanWindow(FiveHours, feed.Limits.GetValueOrDefault(FiveHourKind));
        var week = new PlanWindow(Week, feed.Limits.GetValueOrDefault(WeekKind));

        var tokens = await Task.Run(() =>
        {
            _ledger.Scan();
            return _ledger.Summarize(now, fiveHour.Start(now), week.Start(now));
        });

        return new UsagePage(
            "Claude",
            new WindowLine(fiveHour, tokens.ShortWindow),
            new WindowLine(week, tokens.LongWindow),
            tokens.Today,
            tokens.Total,
            L.Text("Dos históricos locais do Claude encontrados automaticamente."),
            Activity(feed.Latest),
            L.Text("Chat que respondeu por último"),
            feed.LimitsUpdated is { } updated ? L.Format("atualizado {0}", Format.Ago(updated, now)) : L.Text("sem dados ainda"),
            L.Text("Atualizar agora. Os % mudam quando um chat do Claude Code recebe resposta."));
    }

    /// <summary>Each chat of its transcripts: its name in the app (else its folder), and its context as the mod last wrote it.</summary>
    public IReadOnlyList<Conversation> Conversations()
    {
        var contexts = (_lastFeed?.Chats ?? []).Where(chat => chat.Context?.Percent is not null)
            .GroupBy(chat => chat.SessionId).ToDictionary(group => group.Key, group => group.MaxBy(chat => chat.UpdatedAt)!.Context!.Percent);
        return _ledger.Sessions().Select(chat =>
        {
            var title = _titles.Of(chat.Session, () => ChatTitles.Custom(chat.Source.Path, chat.Session)) ?? chat.Source.Project ?? "Claude Code";
            return new Conversation(Id, chat.Session, title, title == chat.Source.Project ? null : chat.Source.Project,
                chat.LastAt, chat.Tokens, contexts.GetValueOrDefault(chat.Session));
        }).ToArray();
    }

    public void Flush() => _ledger.Flush();

    private static string Activity(FeedEntry? latest)
    {
        if (latest is null) return L.Text("Nenhum chat com o mod ainda");

        var parts = new List<string> { ChatTitles.Of(latest) };
        if (latest.Context?.Percent is { } fill) parts.Add(L.Format("contexto {0}", Format.Percent(fill)));
        if (latest.CostUsd is { } cost) parts.Add(Format.Cost(cost));

        return string.Join(" · ", parts);
    }
}
