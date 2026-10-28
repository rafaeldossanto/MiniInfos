using MiniInfos.Providers;
using MiniInfos.Sources;
using MiniInfos.UI.Formatting;

namespace MiniInfos.Providers.Codex;

/// <summary>
/// Codex's page: the account's limits through the codex app-server, and the tokens of the local
/// Codex sessions from their logs.
/// </summary>
public sealed class CodexUsage : IUsageProvider
{
    private static readonly TimeSpan DefaultShortWindow = TimeSpan.FromHours(5);
    private static readonly TimeSpan DefaultLongWindow = TimeSpan.FromDays(7);

    private readonly CodexUsageLedger _ledger = CodexUsageLedger.Load();
    private readonly CodexAccountReader _account = new();
    private readonly TitleCache _titles = new();

    public string Id => "codex";

    public bool IsInstalled() => CodexInstall.IsPresent();

    public bool IsInUse() => CodexApp.IsOpen();

    public async Task<UsagePage> ReadAsync(DateTimeOffset now, bool isShown, bool force)
    {
        if (force) await UsageSources.Shared.RefreshAsync();
        // Reading the limits starts a process that asks OpenAI: only for a page on screen, and
        // waited for only when the person asked; otherwise the page draws the last reading.
        if (force) await _account.RefreshAsync();
        else if (isShown) _account.RefreshIfDue();

        var account = _account.Current;
        var primary = account.Primary ?? new PlanWindow(DefaultShortWindow, null);
        var secondary = account.Secondary ?? new PlanWindow(DefaultLongWindow, null);

        var snapshot = await Task.Run(() =>
        {
            _ledger.Scan();
            return _ledger.Summarize(now, primary.Start(now), secondary.Start(now));
        });

        return new UsagePage(
            "Codex (GPT)",
            new WindowLine(primary, snapshot.Tokens.ShortWindow),
            new WindowLine(secondary, snapshot.Tokens.LongWindow),
            snapshot.Tokens.Today,
            snapshot.Tokens.Total,
            L.Text("Das sessões locais e arquivadas do Codex encontradas automaticamente."),
            Activity(snapshot),
            L.Text("Sessão local mais recente"),
            Updated(account, now),
            L.Text("Atualizar agora os dados locais e os limites da conta Codex."));
    }

    /// <summary>Each local session: its name in the Codex app, else its folder.</summary>
    public IReadOnlyList<Conversation> Conversations() => _ledger.Sessions().Select(session =>
    {
        var name = _titles.Of(session.Id, () => CodexThreadNames.Find(CodexPaths.Roots, session.Id));
        return new Conversation(Id, session.Id, name ?? session.Project ?? "Codex", name is null ? null : session.Project,
            session.LastAt, session.Tokens, session.ContextPercent);
    }).ToArray();

    public void Flush() => _ledger.Flush();

    private static string Activity(CodexUsageSnapshot snapshot)
    {
        if (snapshot.UpdatedAt is null) return L.Text("Nenhuma sessão local do Codex ainda");

        var parts = new List<string> { snapshot.LatestProject ?? L.Text("Sessão Codex") };
        if (snapshot.ContextPercent is { } fill) parts.Add(L.Format("contexto {0}", Format.Percent(fill)));

        return string.Join(" · ", parts);
    }

    private static string Updated(CodexAccountState account, DateTimeOffset now) => account.Status switch
    {
        CodexAccountStatus.Ready when account.UpdatedAt is { } updated => L.Format("atualizado {0}", Format.Ago(updated, now)),
        CodexAccountStatus.NotInstalled => L.Text("Codex não encontrado"),
        _ => L.Text("sem limites ainda"),
    };
}
