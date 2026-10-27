using System.IO;
using System.Text.Json;

namespace MiniInfos.Providers.Gemini;

/// <summary>
/// Reads Gemini CLI's chat recordings. Each message is appended as one JSON line, and a model response is appended
/// again whole once its tokens arrive: {"id","timestamp","type":"gemini","model","tokens":{input,output,cached,thoughts,tool,total}}.
/// Gemini's input already holds the cached part, and thoughts are billed as output.
/// </summary>
public static class GeminiResponses
{
    public static ReadOnlySpan<byte> Marker => "\"tokens\""u8;

    public static LoggedResponse? Read(string path, ReadOnlySpan<byte> line)
    {
        try
        {
            using var document = JsonDocument.Parse(line.ToArray());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("type", out var type) || type.GetString() != "gemini"
                || !root.TryGetProperty("id", out var id) || id.GetString() is not { Length: > 0 } message
                || !root.TryGetProperty("tokens", out var tokens) || tokens.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("timestamp", out var timestamp) || !timestamp.TryGetDateTimeOffset(out var at)) return null;

            var cached = Count(tokens, "cached");
            var entry = new LedgerEntry(
                at.ToUnixTimeSeconds(),
                Math.Max(0, Count(tokens, "input") - cached) + Count(tokens, "tool"),
                Count(tokens, "output") + Count(tokens, "thoughts"),
                cached,
                0);
            var model = root.TryGetProperty("model", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() : null;
            // Message ids are only sure to be unique within their session: the file names the session.
            return new LoggedResponse($"{Path.GetFileNameWithoutExtension(path)}:{message}", entry, model);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static long Count(JsonElement tokens, string name) =>
        tokens.TryGetProperty(name, out var value) && value.TryGetInt64(out var count) ? Math.Max(0, count) : 0;
}
