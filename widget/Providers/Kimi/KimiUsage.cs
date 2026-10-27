using System.IO;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Providers;
using MiniInfos.UI.Formatting;

namespace MiniInfos.Providers.Kimi;

/// <summary>
/// Kimi's page: the tokens of Kimi Code's sessions, from the CLI and from the desktop app's agent. Kimi keeps
/// no reading of the plan's limits on disk, so its windows count the last 5 hours and 7 days with no %.
/// </summary>
public sealed class KimiUsage : IUsageProvider
{
    private static readonly PlanWindow FiveHours = new(TimeSpan.FromHours(5), null);
    private static readonly PlanWindow Week = new(TimeSpan.FromDays(7), null);

    private readonly ResponseLedger _ledger;
    private readonly Func<double?> _context;

    public KimiUsage(Func<IReadOnlyList<string>>? sessions = null, string? storePath = null, Func<double?>? context = null)
    {
        _ledger = new ResponseLedger(sessions ?? (() => KimiPaths.SessionFolders),
            storePath ?? Path.Combine(AppPaths.Data, "kimi-ledger.json"), KimiResponses.Marker, KimiResponses.Read);
        _context = context ?? (() => KimiContext.LatestPercent(KimiPaths.DesktopContext));
    }

    public string Id => "kimi";

    public bool IsInstalled() => KimiInstall.IsPresent();

    public bool IsInUse() => KimiApp.IsOpen();

    public async Task<UsagePage> ReadAsync(DateTimeOffset now, bool isShown, bool force)
    {
        var (tokens, latest, context) = await Task.Run(() =>
        {
            _ledger.Scan();
            var latest = _ledger.Latest;
            return (_ledger.Summarize(now, FiveHours.Start(now), Week.Start(now)), latest,
                latest is not null && KimiPaths.IsDesktop(latest.Path) ? _context() : null);
        });

        return new UsagePage(
            "Kimi",
            new WindowLine(FiveHours, tokens.ShortWindow),
            new WindowLine(Week, tokens.LongWindow),
            tokens.Today,
            tokens.Total,
            L.Text("Das sessões do Kimi Code e do agente do app Kimi encontradas automaticamente."),
            Activity(latest, context),
            L.Text("Sessão que respondeu por último"),
            Format.LastResponse(latest?.At, now),
            L.Text("Atualizar agora os dados locais. O Kimi não guarda os limites do plano no computador."));
    }

    public void Flush() => _ledger.Flush();

    private static string Activity(LatestResponse? latest, double? context)
    {
        if (latest is null) return L.Text("Nenhuma sessão do Kimi Code ainda");

        // The app's agent works in task folders named by time: "App Kimi" says more than the folder's name.
        var parts = new List<string>
        {
            KimiPaths.IsDesktop(latest.Path) ? L.Text("App Kimi") : KimiResponses.Project(latest.Path) ?? "Kimi Code",
        };
        if (context is { } fill) parts.Add(L.Format("contexto {0}", Format.Percent(fill)));

        return string.Join(" · ", parts);
    }
}
