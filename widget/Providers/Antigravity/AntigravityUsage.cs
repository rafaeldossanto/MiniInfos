using System.IO;
using Microsoft.Data.Sqlite;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.UI.Formatting;

namespace MiniInfos.Providers.Antigravity;

/// <summary>The Antigravity agent's local tokens, independent of Gemini CLI and of models selected in the agent.</summary>
public sealed class AntigravityUsage : IUsageProvider
{
    private static readonly PlanWindow FiveHours = new(TimeSpan.FromHours(5), null);
    private static readonly PlanWindow Week = new(TimeSpan.FromDays(7), null);
    private readonly Lock _gate = new();
    private readonly Func<IReadOnlyList<string>> _folders;
    private readonly ResponseLedger _ledger;
    private readonly AntigravityQuotas _quotas;
    private readonly Dictionary<string, (long Length, DateTime Write, long WalLength, DateTime WalWrite)> _scanned = new(SourceFiles.PathComparer);

    public AntigravityUsage(Func<IReadOnlyList<string>>? folders = null, string? storePath = null,
        Func<CancellationToken, Task<string?>>? quotaReader = null)
    {
        _folders = folders ?? (() => AntigravityPaths.ConversationFolders);
        _ledger = new ResponseLedger(() => [], storePath ?? Path.Combine(AppPaths.Data, "antigravity-ledger.json"), [], (_, _) => null);
        _quotas = new AntigravityQuotas(quotaReader);
    }

    public string Id => "antigravity";
    public bool IsInstalled() => AntigravityInstall.IsPresent();
    public bool IsInUse() => AntigravityInstall.IsOpen();

    public async Task<UsagePage> ReadAsync(DateTimeOffset now, bool isShown, bool force)
    {
        var quotaTask = _quotas.ReadAsync(now, isShown, force);
        var (tokens, latest) = await Task.Run(() =>
        {
            lock (_gate)
            {
                Scan(force);
                return (_ledger.Summarize(now, FiveHours.Start(now), Week.Start(now)), _ledger.Latest);
            }
        });
        var snapshot = await quotaTask;
        var active = snapshot?.Buckets.Where(bucket => bucket.Limit.ResetsAtTime > now).ToArray() ?? [];
        // The two-window summary remains useful to the tray/pet; the card presents every independent group.
        var shortQuota = active.Where(bucket => bucket.Length <= TimeSpan.FromDays(1)).OrderByDescending(bucket => bucket.Limit.PercentUsed).FirstOrDefault();
        var longQuota = active.Where(bucket => bucket.Length >= TimeSpan.FromDays(7)).OrderByDescending(bucket => bucket.Limit.PercentUsed).FirstOrDefault();
        var shortTokens = tokens.ShortWindow;
        if (shortQuota?.Length == TimeSpan.FromDays(1))
            shortTokens = _ledger.Summarize(now, now.AddDays(-1), now.AddDays(-7)).ShortWindow;
        var groups = active.GroupBy(bucket => bucket.Group).Select(group => new UsageQuotaGroup(
            group.Key == "Gemini Models" ? "Gemini" : group.Key == "Claude and GPT models" ? L.Text("Claude e GPT") : group.Key,
            group.Key switch { "Gemini Models" => "Gemini Pro · Gemini Flash", "Claude and GPT models" => "Opus · Sonnet · GPT OSS", _ => null },
            group.OrderBy(bucket => bucket.Length).Select(bucket => new PlanWindow(bucket.Length, bucket.Limit)).ToArray()))
            .OrderBy(group => group.Windows.Count).ToArray();
        return new UsagePage("Antigravity", Line(shortQuota, FiveHours, shortTokens), Line(longQuota, Week, tokens.LongWindow),
            tokens.Today, tokens.Total, L.Text("Dos registros locais do Antigravity encontrados automaticamente."),
            groups.Length > 0 ? "" : L.Text("Abra o Antigravity para consultar os limites da conta."),
            L.Text("Os tokens incluem todos os modelos usados no Antigravity."),
            snapshot is not null ? L.Format("limites {0}", Format.Ago(snapshot.At, now)) : Format.LastResponse(latest?.At, now),
            L.Text("Atualizar os tokens locais e consultar os limites pelo Antigravity aberto."), groups);
    }

    private static WindowLine Line(AntigravityQuota? quota, PlanWindow fallback, TokenCount tokens)
    {
        if (quota is null) return new WindowLine(fallback, tokens);
        var label = quota.Length == TimeSpan.FromDays(1) ? L.Text("Diário") : Format.WindowLabel(quota.Length);
        var group = quota.Group switch { "Gemini Models" => "Gemini", "Claude and GPT models" => "Claude/GPT", _ => quota.Group };
        return new WindowLine(new PlanWindow(quota.Length, quota.Limit), tokens, label + " · " + group);
    }

    private void Scan(bool force)
    {
        foreach (var path in _folders().Distinct(SourceFiles.PathComparer).SelectMany(folder => SourceFiles.Files(folder, "*.db")))
        {
            if (!Guid.TryParse(Path.GetFileNameWithoutExtension(path), out _)) continue;
            try
            {
                var file = new FileInfo(path);
                var wal = new FileInfo(path + "-wal");
                var stamp = (file.Length, file.LastWriteTimeUtc, wal.Exists ? wal.Length : 0, wal.Exists ? wal.LastWriteTimeUtc : DateTime.MinValue);
                if (!force && _scanned.TryGetValue(path, out var previous) && previous == stamp) continue;
                _ledger.Import(path, AntigravityDatabase.Read(path));
                _scanned[path] = stamp;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or SqliteException)
            {
                // A locked, missing or newer database cannot erase already counted usage. Retry next scan.
            }
        }
    }

    public IReadOnlyList<Conversation> Conversations() => _ledger.Sessions()
        .Select(chat => new Conversation(Id, chat.Session, chat.Source.Project ?? "Antigravity", null, chat.LastAt, chat.Tokens)).ToArray();
    public void Flush() => _ledger.Flush();
}
