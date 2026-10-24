using System.IO;
using System.Text.Json;

namespace MiniInfos;

public static class ChatTitles
{
    /// <summary>The chat's name as given in the app, else the name of the folder it runs in.</summary>
    public static string Of(FeedEntry entry) =>
        FindCustomTitle(entry.SessionId)
        ?? (entry.Cwd is { Length: > 0 } cwd ? Path.GetFileName(cwd.TrimEnd('\\', '/')) : "chat");

    private static string? FindCustomTitle(string sessionId)
    {
        if (!Directory.Exists(ClaudePaths.Projects)) return null;

        foreach (var project in Directory.EnumerateDirectories(ClaudePaths.Projects))
        {
            var file = Path.Combine(project, sessionId, "custom-title.json");
            if (!File.Exists(file)) continue;

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                if (document.RootElement.TryGetProperty("customTitle", out var title) && title.GetString() is { Length: > 0 } name)
                {
                    return name;
                }
            }
            catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
            {
            }
        }

        return null;
    }
}
