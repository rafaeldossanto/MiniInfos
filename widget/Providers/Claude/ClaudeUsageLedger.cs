using System.IO;
using System.Text.Json;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Providers;

namespace MiniInfos.Providers.Claude;

/// <summary>
/// Every model response of every Claude Code chat, read from the transcripts under
/// ~/.claude/projects and kept in a ledger of its own, so the totals survive the
/// transcripts being cleaned up. Each file is read from where the last scan stopped.
/// </summary>
public sealed class ClaudeUsageLedger
{
    private readonly ResponseLedger _ledger;

    private ClaudeUsageLedger(Func<IReadOnlyList<string>> projects, string storePath) =>
        _ledger = new ResponseLedger(projects, storePath, "\"usage\""u8, Read);

    public static ClaudeUsageLedger Load(string? projectsPath = null, string? storePath = null) => new(
        projectsPath is null ? () => ClaudePaths.ProjectFolders : () => [projectsPath],
        storePath ?? Path.Combine(AppPaths.Data, "ledger.json"));

    public static ClaudeUsageLedger LoadSources(Func<IReadOnlyList<string>> projects, string storePath) => new(projects, storePath);

    public void Scan() => _ledger.Scan();

    public TokenSummary Summarize(DateTimeOffset now, DateTimeOffset shortWindowStart, DateTimeOffset longWindowStart) =>
        _ledger.Summarize(now, shortWindowStart, longWindowStart);

    public IReadOnlyList<SessionTally> Sessions() => _ledger.Sessions();

    public void Flush() => _ledger.Flush();

    /// <summary>One response is written once per content block, all under the message's id, with its chat's id and folder.</summary>
    private static LoggedResponse? Read(string path, ReadOnlySpan<byte> line)
    {
        try
        {
            using var document = JsonDocument.Parse(line.ToArray());
            var root = document.RootElement;

            if (!root.TryGetProperty("type", out var type) || type.GetString() != "assistant") return null;
            if (!root.TryGetProperty("message", out var message)) return null;
            if (!message.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object) return null;
            if (!message.TryGetProperty("id", out var id) || id.GetString() is not { Length: > 0 } key) return null;
            if (!root.TryGetProperty("timestamp", out var timestamp) || !timestamp.TryGetDateTimeOffset(out var at)) return null;

            var cwd = ReadText(root, "cwd");
            return new LoggedResponse(key, new LedgerEntry(
                at.ToUnixTimeSeconds(),
                ReadCount(usage, "input_tokens"),
                ReadCount(usage, "output_tokens"),
                ReadCount(usage, "cache_read_input_tokens"),
                ReadCount(usage, "cache_creation_input_tokens")),
                Session: ReadText(root, "sessionId"),
                Project: cwd is null ? null : SourceFiles.LeafName(cwd));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static long ReadCount(JsonElement usage, string name) =>
        usage.TryGetProperty(name, out var value) && value.TryGetInt64(out var count) ? count : 0;

    private static string? ReadText(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            && property.GetString() is { Length: > 0 } text ? text : null;
}
