using System.IO;
using System.Text.Json;

namespace MiniInfos;

public readonly record struct TokenCount(long Input, long Output, long CacheRead, long CacheWrite)
{
    public long Total => Input + Output + CacheRead + CacheWrite;

    public static TokenCount operator +(TokenCount left, TokenCount right) => new(
        left.Input + right.Input,
        left.Output + right.Output,
        left.CacheRead + right.CacheRead,
        left.CacheWrite + right.CacheWrite);
}

public sealed record LedgerEntry(long At, long Input, long Output, long CacheRead, long CacheWrite)
{
    public TokenCount Count => new(Input, Output, CacheRead, CacheWrite);
}

public sealed record TokenSummary(TokenCount Today, TokenCount FiveHour, TokenCount Week, TokenCount Total);

/// <summary>
/// Every model response of every Claude Code chat, read from the transcripts under
/// ~/.claude/projects and kept in a ledger of its own, so the totals survive the
/// transcripts being cleaned up. Each file is read from where the last scan stopped.
/// </summary>
public sealed class UsageLedger
{
    private static readonly byte[] UsageMarker = "\"usage\""u8.ToArray();
    private static readonly string StorePath = Path.Combine(ClaudePaths.AppData, "ledger.json");
    private static readonly TimeSpan SaveEvery = TimeSpan.FromSeconds(30);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, long> _offsets;
    private readonly Dictionary<string, LedgerEntry> _messages;
    private DateTimeOffset _savedAt = DateTimeOffset.MinValue;
    private bool _isDirty;

    private UsageLedger(Dictionary<string, long> offsets, Dictionary<string, LedgerEntry> messages)
    {
        _offsets = offsets;
        _messages = messages;
    }

    public static UsageLedger Load()
    {
        try
        {
            var stored = JsonSerializer.Deserialize<StoredLedger>(File.ReadAllText(StorePath));
            if (stored is not null) return new UsageLedger(stored.Offsets, stored.Messages);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
        }

        return new UsageLedger([], []);
    }

    public void Scan()
    {
        lock (_gate)
        {
            if (!Directory.Exists(ClaudePaths.Projects)) return;

            var seen = new HashSet<string>();
            foreach (var file in Directory.EnumerateFiles(ClaudePaths.Projects, "*.jsonl", SearchOption.AllDirectories))
            {
                seen.Add(file);
                ScanFile(file);
            }

            foreach (var gone in _offsets.Keys.Where(path => !seen.Contains(path)).ToList())
            {
                _offsets.Remove(gone);
            }

            if (_isDirty && DateTimeOffset.Now - _savedAt >= SaveEvery) Save();
        }
    }

    public TokenSummary Summarize(DateTimeOffset todayStart, DateTimeOffset fiveHourStart, DateTimeOffset weekStart)
    {
        lock (_gate)
        {
            var today = todayStart.ToUnixTimeSeconds();
            var fiveHour = fiveHourStart.ToUnixTimeSeconds();
            var week = weekStart.ToUnixTimeSeconds();
            TokenCount todayCount = default, fiveHourCount = default, weekCount = default, total = default;

            foreach (var entry in _messages.Values)
            {
                var count = entry.Count;
                total += count;
                if (entry.At >= today) todayCount += count;
                if (entry.At >= fiveHour) fiveHourCount += count;
                if (entry.At >= week) weekCount += count;
            }

            return new TokenSummary(todayCount, fiveHourCount, weekCount, total);
        }
    }

    public void Flush()
    {
        lock (_gate)
        {
            if (_isDirty) Save();
        }
    }

    private void ScanFile(string path)
    {
        var offset = _offsets.GetValueOrDefault(path);

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < offset) offset = 0;
            if (stream.Length == offset) return;

            stream.Seek(offset, SeekOrigin.Begin);
            var buffer = new byte[checked((int)(stream.Length - offset))];
            stream.ReadExactly(buffer);

            var end = Array.LastIndexOf(buffer, (byte)'\n');
            if (end < 0) return;

            var rest = buffer.AsSpan(0, end + 1);
            while (!rest.IsEmpty)
            {
                var newline = rest.IndexOf((byte)'\n');
                var line = rest[..newline];
                rest = rest[(newline + 1)..];
                if (line.IndexOf(UsageMarker) >= 0) Record(line);
            }

            _offsets[path] = offset + end + 1;
            _isDirty = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Busy or gone right now: the next scan tries again from the same offset.
        }
    }

    private void Record(ReadOnlySpan<byte> line)
    {
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

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(ClaudePaths.AppData);
            var temporary = StorePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new StoredLedger(_offsets, _messages)));
            File.Move(temporary, StorePath, overwrite: true);
            _savedAt = DateTimeOffset.Now;
            _isDirty = false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record StoredLedger(Dictionary<string, long> Offsets, Dictionary<string, LedgerEntry> Messages);
}
