using System.IO;
using System.Text.Json;

namespace MiniInfos.Providers.Kimi;

/// <summary>
/// Reads Kimi Code's wire.jsonl: every model turn appends a usage.record with its billed tokens. Each agent
/// (the main one and every subagent) logs only its own turns in its own wire, so summing the wires counts each once.
/// </summary>
public static class KimiResponses
{
    public static ReadOnlySpan<byte> Marker => "\"usage.record\""u8;

    public static LoggedResponse? Read(string path, ReadOnlySpan<byte> line)
    {
        try
        {
            using var document = JsonDocument.Parse(line.ToArray());
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var type) || type.GetString() != "usage.record") return null;
            if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("time", out var time) || !time.TryGetInt64(out var milliseconds)) return null;

            var entry = new LedgerEntry(
                DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToUnixTimeSeconds(),
                Count(usage, "inputOther"),
                Count(usage, "output"),
                Count(usage, "inputCacheRead"),
                Count(usage, "inputCacheCreation"));
            var model = root.TryGetProperty("model", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() : null;
            return new LoggedResponse($"{Agent(path)}:{milliseconds}:{entry.Count.Total}", entry, model, KimiSessions.Key(path));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>The session's folder: .../sessions/wd_*/&lt;session&gt;/agents/&lt;agent&gt;/wire.jsonl.</summary>
    public static string? SessionFolder(string wirePath) => Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(wirePath)));

    /// <summary>The folder the session worked in, by name, from its state.json; null when it has none.</summary>
    public static string? Project(string wirePath) => SessionFolder(wirePath) is { } session ? KimiSessions.Project(session) : null;

    /// <summary>Session and agent, which name a turn together with its time.</summary>
    private static string Agent(string path)
    {
        var agent = Path.GetDirectoryName(path);
        return $"{Path.GetFileName(SessionFolder(path))}/{Path.GetFileName(agent)}";
    }

    private static long Count(JsonElement usage, string name) =>
        usage.TryGetProperty(name, out var value) && value.TryGetInt64(out var count) ? Math.Max(0, count) : 0;
}
