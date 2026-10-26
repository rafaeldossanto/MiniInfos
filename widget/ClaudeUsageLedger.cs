using System.IO;
using System.Text.Json;

namespace MiniInfos;

/// <summary>
/// Every model response of every Claude Code chat, read from the transcripts under
/// ~/.claude/projects and kept in a ledger of its own, so the totals survive the
/// transcripts being cleaned up. Each file is read from where the last scan stopped.
/// </summary>
public sealed class ClaudeUsageLedger
{
    private static readonly byte[] UsageMarker = "\"usage\""u8.ToArray();

    private readonly Lock _gate = new();
    private readonly Func<IReadOnlyList<string>> _projects;
    private readonly LedgerStore<StoredLedger> _store;
    private readonly Dictionary<string, long> _offsets;
    private readonly Dictionary<string, LedgerEntry> _messages;

    private ClaudeUsageLedger(Func<IReadOnlyList<string>> projects, string storePath)
    {
        _projects = projects;
        _store = new LedgerStore<StoredLedger>(storePath);
        var stored = _store.Load();
        _offsets = stored?.Offsets ?? [];
        _messages = stored?.Messages ?? [];
    }

    public static ClaudeUsageLedger Load(string? projectsPath = null, string? storePath = null) => new(
        projectsPath is null ? () => ClaudePaths.ProjectFolders : () => [projectsPath],
        storePath ?? Path.Combine(AppPaths.Data, "ledger.json"));

    public static ClaudeUsageLedger LoadSources(Func<IReadOnlyList<string>> projects, string storePath) => new(projects, storePath);

    public void Scan()
    {
        lock (_gate)
        {
            JsonlTail.ReadFolders(_projects(), _offsets, ReadFile);
            _store.SaveIfDue(Stored());
        }
    }

    public TokenSummary Summarize(DateTimeOffset now, DateTimeOffset shortWindowStart, DateTimeOffset longWindowStart)
    {
        lock (_gate)
        {
            return TokenSummary.Of(_messages.Values, now, shortWindowStart, longWindowStart);
        }
    }

    public void Flush()
    {
        lock (_gate)
        {
            _store.Flush(Stored());
        }
    }

    private void ReadFile(string path)
    {
        var offset = _offsets.GetValueOrDefault(path);
        var next = JsonlTail.Read(path, offset, Record);
        if (next == offset) return;

        _offsets[path] = next;
        _store.Changed();
    }

    private void Record(ReadOnlySpan<byte> line)
    {
        if (line.IndexOf(UsageMarker) < 0) return;

        try
        {
            using var document = JsonDocument.Parse(line.ToArray());
            var root = document.RootElement;

            if (!root.TryGetProperty("type", out var type) || type.GetString() != "assistant") return;
            if (!root.TryGetProperty("message", out var message)) return;
            if (!message.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object) return;
            if (!message.TryGetProperty("id", out var id) || id.GetString() is not { Length: > 0 } key) return;
            if (!root.TryGetProperty("timestamp", out var timestamp) || !timestamp.TryGetDateTimeOffset(out var at)) return;

            var entry = new LedgerEntry(
                at.ToUnixTimeSeconds(),
                ReadCount(usage, "input_tokens"),
                ReadCount(usage, "output_tokens"),
                ReadCount(usage, "cache_read_input_tokens"),
                ReadCount(usage, "cache_creation_input_tokens"));

            // One response is written once per content block: keep the fullest count of it.
            if (_messages.TryGetValue(key, out var known) && known.Count.Total >= entry.Count.Total) return;
            _messages[key] = entry;
        }
        catch (JsonException)
        {
        }
    }

    private static long ReadCount(JsonElement usage, string name) =>
        usage.TryGetProperty(name, out var value) && value.TryGetInt64(out var count) ? count : 0;

    private StoredLedger Stored() => new(_offsets, _messages);

    /// <summary>The file's shape since 1.0.0: offsets by transcript path, and the messages counted.</summary>
    private sealed record StoredLedger(Dictionary<string, long> Offsets, Dictionary<string, LedgerEntry> Messages);
}
