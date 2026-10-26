using System.IO;
using System.Text.Json;

namespace MiniInfos;

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
    private readonly string _sessionsPath;
    private readonly LedgerStore<StoredLedger> _store;
    private readonly Dictionary<string, Cursor> _cursors;
    private readonly Dictionary<string, LedgerEntry> _readings;

    private CodexUsageLedger(string sessionsPath, string storePath)
    {
        _sessionsPath = sessionsPath;
        _store = new LedgerStore<StoredLedger>(storePath);
        var stored = _store.Load();
        _cursors = stored?.Cursors ?? [];
        _readings = stored?.Readings ?? [];
    }

    public static CodexUsageLedger Load(string? sessionsPath = null, string? storePath = null) => new(
        sessionsPath ?? CodexPaths.Sessions,
        storePath ?? Path.Combine(AppPaths.Data, "codex-ledger.json"));

    public void Scan()
    {
        lock (_gate)
        {
            JsonlTail.ReadFolder(_sessionsPath, _cursors, ReadFile);
            _store.SaveIfDue(Stored());
        }
    }

    public CodexUsageSnapshot Summarize(DateTimeOffset now, DateTimeOffset shortWindowStart, DateTimeOffset longWindowStart)
    {
        lock (_gate)
        {
            var latest = _cursors.Values.MaxBy(cursor => cursor.LastAt);
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
        var cursor = known;
        var next = JsonlTail.Read(
            path,
            known.Offset,
            line => cursor = Record(path, cursor, line),
            onRestart: () => cursor = new Cursor());
        if (next == known.Offset && ReferenceEquals(cursor, known)) return;

        _cursors[path] = cursor with { Offset = next };
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
                return cursor with { Project = cwd is null ? null : Path.GetFileName(Path.TrimEndingDirectorySeparator(cwd)) };
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
            _readings[$"{path}:{totals.Total}"] = new LedgerEntry(
                at.ToUnixTimeSeconds(), UncachedInput(delta), delta.Output, delta.CacheRead, delta.CacheWrite);

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

    /// <summary>
    /// Codex's input counts its cached part too, and some readings (a compaction's) carry only
    /// total_tokens: the uncached input is what the total leaves after output and cache, so the
    /// entry adds up to total_tokens whichever parts a reading fills in.
    /// </summary>
    private static long UncachedInput(Totals delta) =>
        Math.Max(0, Math.Max(delta.Input, delta.Total - delta.Output) - delta.CacheRead - delta.CacheWrite);

    private static long ReadLong(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out var number) ? number : 0;

    private static string? ReadString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() : null;

    private StoredLedger Stored() => new(_cursors, _readings);

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
        public long Offset { get; init; }
        public Totals Totals { get; init; } = new(0, 0, 0, 0, 0);
        public long LastAt { get; init; }
        public string? Project { get; init; }
        public double? ContextPercent { get; init; }
    }

    private sealed record StoredLedger(Dictionary<string, Cursor> Cursors, Dictionary<string, LedgerEntry> Readings);
}
