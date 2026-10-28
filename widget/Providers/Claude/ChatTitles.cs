using System.IO;
using System.Text.Json;
using MiniInfos.Infrastructure.Storage;

namespace MiniInfos.Providers.Claude;

public static class ChatTitles
{
    /// <summary>The chat's name as given in the app, else the name of the folder it runs in.</summary>
    public static string Of(FeedEntry entry) =>
        FindCustomTitle(entry.SessionId)
        ?? (entry.Cwd is { Length: > 0 } cwd ? MiniInfos.Infrastructure.Storage.SourceFiles.LeafName(cwd) : "chat");

    /// <summary>The name given in the app to the chat of this transcript: its custom-title.json sits beside it, in the chat's folder.</summary>
    public static string? Custom(string transcriptPath, string sessionId) =>
        Path.GetDirectoryName(transcriptPath) is { } project ? Read(Path.Combine(project, sessionId, "custom-title.json")) : null;

    private static string? FindCustomTitle(string sessionId) =>
        ClaudePaths.ProjectFolders.SelectMany(root => SourceFiles.Directories(root))
            .Select(project => Read(Path.Combine(project, sessionId, "custom-title.json")))
            .FirstOrDefault(title => title is not null);

    private static string? Read(string file)
    {
        if (!File.Exists(file)) return null;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            return document.RootElement.TryGetProperty("customTitle", out var title) && title.GetString() is { Length: > 0 } name ? name : null;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
