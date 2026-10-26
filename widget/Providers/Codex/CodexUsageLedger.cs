using System.IO;
using System.Text.Json;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Providers;

namespace MiniInfos.Providers.Codex;

public sealed record CodexUsageSnapshot(
    TokenSummary Tokens,
    string? LatestProject,
    double? ContextPercent,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// Counts the differences between cumulative token readings in Codex session logs. The cursor
/// and ledger survive log cleanup, while a repeated reading cannot add tokens twice.
/// </summary>
public sealed class CodexUsageLedger
{
    private static readonly byte[] TokenMarker = "\"token_count\""u8.ToArray();
    private static readonly byte[] SessionMarker = "\"session_meta\""u8.ToArray();

    private readonly Lock _gate = new();
    private readonly Func<IReadOnlyList<string>> _sources;
    private readonly LedgerStore<StoredLedger> _store;
    private readonly Dictionary<string, Cursor> _cursors;
    private readonly Dictionary<string, LedgerEntry> _readings;
    private readonly Dictionary<string, Cursor> _sessions;

    private CodexUsageLedger(Func<IReadOnlyList<string>> sources, string storePath)
    {
        _sources = sources;
        _store = new LedgerStore<StoredLedger>(storePath);
        var stored = _store.Load();
        _cursors = stored?.Cursors ?? [];
        _readings = stored?.Readings ?? [];
        _sessions = stored?.Sessions ?? [];
        foreach (var (path, cursor) in _cursors) Remember(cursor with { SessionId = cursor.SessionId ?? SessionKey(path) });
        if (stored is not null && stored.Sessions is null) { RecoverSessions(); _store.Changed(); }
    }

    public static CodexUsageLedger Load(string? sessionsPath = null, string? storePath = null) => new(
        sessionsPath is null ? () => CodexPaths.SessionFolders : () => [sessionsPath],
        storePath ?? Path.Combine(AppPaths.Data, "codex-ledger.json"));

    public static CodexUsageLedger LoadSources(Func<IReadOnlyList<string>> sources, string storePath) => new(sources, storePath);

    public void Scan()
    {
        lock (_gate)
        {
            JsonlTail.ReadFolders(_sources(), _cursors, ReadFile);
            _store.SaveIfDue(Stored());
        }
    }

    public CodexUsageSnapshot Summarize(DateTimeOffset now, DateTimeOffset shortWindowStart, DateTimeOffset longWindowStart)
    {
        lock (_gate)
        {
            var latest = _sessions.Values.MaxBy(cursor => cursor.LastAt);
            DateTimeOffset? updated = latest is { LastAt: > 0 } ? DateTimeOffset.FromUnixTimeSeconds(latest.LastAt) : null;

            return new CodexUsageSnapshot(
                TokenSummary.Of(_readings.Values, now, shortWindowStart, longWindowStart),
                latest?.Project,
                latest?.ContextPercent,
                updated);
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
        var known = _cursors.GetValueOrDefault(path) ?? new Cursor();
        var id = known.SessionId ?? SessionKey(path);
        var cursor = Rebase(known with { SessionId = id });
        var next = JsonlTail.Read(
            path,
            known.Offset,
            line => cursor = Record(path, cursor, line),
            onRestart: () => cursor = Rebase(new Cursor { SessionId = id }));
        if (next == known.Offset && cursor == known) return;

        _cursors[path] = cursor with { Offset = next };
        Remember(cursor);
        _store.Changed();
    }

    private Cursor Record(string path, Cursor cursor, ReadOnlySpan<byte> line)
    {
        if (line.IndexOf(TokenMarker) < 0 && line.IndexOf(SessionMarker) < 0) return cursor;

        try
        {
            using var document = JsonDocument.Parse(line.ToArray());
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var type) || !root.TryGetProperty("payload", out var payload)) return cursor;

            if (type.GetString() == "session_meta")
            {
                var cwd = ReadString(payload, "cwd");
                var id = ReadString(payload, "id") ?? cursor.SessionId ?? SessionKey(path);
                if (Guid.TryParse(id, out var guid)) id = guid.ToString("D");
                return Rebase(cursor with { SessionId = id, Project = cwd is null ? null : Path.GetFileName(Path.TrimEndingDirectorySeparator(cwd)) });
            }

            if (type.GetString() != "event_msg" || !payload.TryGetProperty("type", out var payloadType)
                || payloadType.GetString() != "token_count") return cursor;
            if (!payload.TryGetProperty("info", out var info) || info.ValueKind != JsonValueKind.Object
                || !info.TryGetProperty("total_token_usage", out var total) || total.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("timestamp", out var timestamp)
                || !timestamp.TryGetDateTimeOffset(out var at)) return cursor;

            var totals = new Totals(
                ReadLong(total, "input_tokens"),
                ReadLong(total, "cached_input_tokens"),
                ReadLong(total, "cache_write_input_tokens"),
                ReadLong(total, "output_tokens"),
                ReadLong(total, "total_tokens"));
            if (totals.Total <= cursor.Totals.Total) return cursor;

            var delta = totals - cursor.Totals;
            // The ID belongs to the session, not its directory. A moved/copied file only contributes growth.
            var output = Math.Min(delta.Output, delta.Total);
            var cacheRead = Math.Min(delta.CacheRead, delta.Total - output);
            var cacheWrite = Math.Min(delta.CacheWrite, delta.Total - output - cacheRead);
            _readings[$"session:{cursor.SessionId}:{totals.Total}"] = new LedgerEntry(
                at.ToUnixTimeSeconds(), delta.Total - output - cacheRead - cacheWrite, output, cacheRead, cacheWrite);

            double? contextPercent = cursor.ContextPercent;
            if (info.TryGetProperty("last_token_usage", out var last) && last.ValueKind == JsonValueKind.Object
                && info.TryGetProperty("model_context_window", out var window) && window.TryGetInt64(out var size) && size > 0)
                contextPercent = Math.Clamp(ReadLong(last, "input_tokens") * 100d / size, 0, 100);

            return cursor with { Totals = totals, LastAt = at.ToUnixTimeSeconds(), ContextPercent = contextPercent };
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return cursor;
        }
    }

    /// <summary>A second path for a known session starts from the session's greatest saved reading.</summary>
    private Cursor Rebase(Cursor cursor) => cursor.SessionId is { } id && _sessions.TryGetValue(id, out var session)
        && session.Totals.Total > cursor.Totals.Total
            ? cursor with { Totals = session.Totals, LastAt = session.LastAt, Project = cursor.Project ?? session.Project, ContextPercent = session.ContextPercent }
            : cursor;

    private void Remember(Cursor cursor)
    {
        if (cursor.SessionId is not { } id) return;
        if (!_sessions.TryGetValue(id, out var previous) || previous.Totals.Total < cursor.Totals.Total) _sessions[id] = cursor;
    }

    private static string SessionKey(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.Length >= 36 && Guid.TryParse(name[^36..], out var id) ? id.ToString("D") : Path.GetFullPath(path).ToUpperInvariant();
    }

    /// <summary>Old ledgers may have dropped a moved file's cursor; recover its high-water mark from its saved entries.</summary>
    private void RecoverSessions()
    {
        var files = new Dictionary<string, (long Total, TokenCount Counts, long At)>();
        foreach (var (key, reading) in _readings)
        {
            var separator = key.LastIndexOf(':');
            if (separator < 0 || !long.TryParse(key[(separator + 1)..], out var total)) continue;
            var prefix = key[..separator];
            var known = files.GetValueOrDefault(prefix);
            files[prefix] = (Math.Max(total, known.Total), known.Counts + reading.Count, Math.Max(known.At, reading.At));
        }
        foreach (var (prefix, known) in files)
        {
            var id = prefix.StartsWith("session:", StringComparison.Ordinal) ? prefix[8..] : SessionKey(prefix);
            Remember(new Cursor
            {
                SessionId = id, LastAt = known.At,
                Totals = new(known.Counts.Input + known.Counts.CacheRead + known.Counts.CacheWrite,
                    known.Counts.CacheRead, known.Counts.CacheWrite, known.Counts.Output, known.Total),
            });
        }
    }

    private static long ReadLong(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out var number) ? number : 0;

    private static string? ReadString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() : null;

    private StoredLedger Stored() => new(_cursors, _readings, _sessions);

    private sealed record Totals(long Input, long CacheRead, long CacheWrite, long Output, long Total)
    {
        public static Totals operator -(Totals current, Totals previous) => new(
            Math.Max(0, current.Input - previous.Input),
            Math.Max(0, current.CacheRead - previous.CacheRead),
            Math.Max(0, current.CacheWrite - previous.CacheWrite),
            Math.Max(0, current.Output - previous.Output),
            Math.Max(0, current.Total - previous.Total));
    }

    private sealed record Cursor
    {
        public string? SessionId { get; init; }
        public long Offset { get; init; }
        public Totals Totals { get; init; } = new(0, 0, 0, 0, 0);
        public long LastAt { get; init; }
        public string? Project { get; init; }
        public double? ContextPercent { get; init; }
    }

    private sealed record StoredLedger(Dictionary<string, Cursor> Cursors, Dictionary<string, LedgerEntry> Readings, Dictionary<string, Cursor>? Sessions = null);
}
