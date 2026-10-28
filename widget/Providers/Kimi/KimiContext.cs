using System.IO;
using System.Text.Json;

namespace MiniInfos.Providers.Kimi;

/// <summary>
/// How full the desktop agent's latest conversation is, as the app saves it for its own meter:
/// {"&lt;conversation&gt;": {"contextTokens": …, "maxContextTokens": …, "updatedAt": "…"}}.
/// </summary>
public static class KimiContext
{
    public static double? LatestPercent(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;

            double? latest = null;
            var latestAt = DateTimeOffset.MinValue;
            foreach (var conversation in document.RootElement.EnumerateObject())
            {
                var value = conversation.Value;
                if (value.ValueKind != JsonValueKind.Object
                    || !value.TryGetProperty("contextTokens", out var used) || !used.TryGetInt64(out var tokens)
                    || !value.TryGetProperty("maxContextTokens", out var max) || !max.TryGetInt64(out var size) || size <= 0
                    || !value.TryGetProperty("updatedAt", out var updated) || !updated.TryGetDateTimeOffset(out var at)
                    || at < latestAt) continue;

                latestAt = at;
                latest = Math.Clamp(tokens * 100d / size, 0, 100);
            }

            return latest;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
