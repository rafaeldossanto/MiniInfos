using System.IO;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Providers;
using MiniInfos.UI.Formatting;

namespace MiniInfos.Providers.Gemini;

/// <summary>
/// Gemini's page: the tokens of Gemini CLI's chats. Gemini keeps no reading of the account's quota on disk, so its
/// windows count the last 5 hours and 7 days with no %.
/// </summary>
public sealed class GeminiUsage : IUsageProvider
{
    private static readonly PlanWindow FiveHours = new(TimeSpan.FromHours(5), null);
    private static readonly PlanWindow Week = new(TimeSpan.FromDays(7), null);

    private readonly ResponseLedger _ledger;

    public GeminiUsage(Func<IReadOnlyList<string>>? chats = null, string? storePath = null) =>
        _ledger = new ResponseLedger(chats ?? (() => GeminiPaths.ChatFolders),
            storePath ?? Path.Combine(AppPaths.Data, "gemini-ledger.json"), GeminiResponses.Marker, GeminiResponses.Read);

    public string Id => "gemini";

    public bool IsInstalled() => GeminiInstall.IsPresent();

    /// <summary>Gemini CLI runs in a terminal with no window of its own to follow.</summary>
    public bool IsInUse() => false;

    public async Task<UsagePage> ReadAsync(DateTimeOffset now, bool isShown, bool force)
    {
        var (tokens, latest) = await Task.Run(() =>
        {
            _ledger.Scan();
            return (_ledger.Summarize(now, FiveHours.Start(now), Week.Start(now)), _ledger.Latest);
        });

        return new UsagePage(
            "Gemini",
            new WindowLine(FiveHours, tokens.ShortWindow),
            new WindowLine(Week, tokens.LongWindow),
            tokens.Today,
            tokens.Total,
            L.Text("Das conversas do Gemini CLI encontradas automaticamente."),
            latest is null ? L.Text("Nenhuma conversa do Gemini CLI ainda") : latest.Label ?? "Gemini CLI",
            L.Text("Modelo que respondeu por último"),
            Format.LastResponse(latest?.At, now),
            L.Text("Atualizar agora os dados locais. O Gemini não guarda a cota da conta no computador."));
    }

    /// <summary>Each chat by its project's folder: Gemini CLI keeps no name for its chats.</summary>
    public IReadOnlyList<Conversation> Conversations() => _ledger.Sessions()
        .Select(chat => new Conversation(Id, chat.Session, chat.Source.Project ?? "Gemini CLI", null, chat.LastAt, chat.Tokens))
        .ToArray();

    public void Flush() => _ledger.Flush();
}
