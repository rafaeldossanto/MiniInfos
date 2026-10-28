using System.IO;
using System.Text.Json;

namespace MiniInfos.Providers.Codex;

/// <summary>
/// The names the Codex app gives its threads, from session_index.jsonl in each of its homes:
/// {"id","thread_name","updated_at"} per line, the last line for an id being its current name.
/// </summary>
public static class CodexThreadNames
{
    public static string? Find(IEnumerable<string> roots, string sessionId)
    {
        string? name = null;
        foreach (var root in roots)
        {
            var index = Path.Combine(root, "session_index.jsonl");
            if (!File.Exists(index)) continue;

            try
            {
                foreach (var line in File.ReadLines(index))
                {
                    if (!line.Contains(sessionId, StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        using var document = JsonDocument.Parse(line);
                        var thread = document.RootElement;
                        if (thread.TryGetProperty("id", out var id) && string.Equals(id.GetString(), sessionId, StringComparison.OrdinalIgnoreCase)
                            && thread.TryGetProperty("thread_name", out var title) && title.GetString() is { Length: > 0 } text) name = text;
                    }
                    catch (JsonException)
                    {
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        return name;
    }
}
