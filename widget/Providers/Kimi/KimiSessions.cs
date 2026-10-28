using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using MiniInfos.Infrastructure.Storage;

namespace MiniInfos.Providers.Kimi;

/// <summary>
/// Kimi's conversations. In the desktop app one lives in a task folder (wd_*) holding its session and a helper
/// session (ctitle-*) that only names it, so the folder is the conversation; in Kimi Code each session is one.
/// Their state.json says the title (the first request, as the app tags it) and the folder they worked in.
/// </summary>
public static partial class KimiSessions
{
    private const string TitleHelper = "ctitle-";

    /// <summary>The conversation a wire.jsonl belongs to, as a folder: the task folder in the app, the session in Kimi Code.</summary>
    public static string Key(string wirePath)
    {
        var session = KimiResponses.SessionFolder(wirePath) ?? wirePath;
        return KimiPaths.IsDesktop(wirePath) ? Path.GetDirectoryName(session) ?? session : session;
    }

    /// <summary>The conversation's first request without the app's tags, which reads as its name.</summary>
    public static string? Title(string conversation) => States(conversation)
        .Select(state => Read(state, "title") is { } title ? Tags().Replace(title, " ").Trim() : null)
        .FirstOrDefault(title => title is { Length: > 0 });

    public static string? Project(string conversation) => States(conversation)
        .Select(state => Read(state, "workDir"))
        .Where(folder => folder is not null)
        .Select(folder => SourceFiles.LeafName(folder!))
        .FirstOrDefault();

    /// <summary>A session's own state.json, or those of a task folder's sessions, the naming helper's left out.</summary>
    private static IEnumerable<string> States(string conversation)
    {
        var own = Path.Combine(conversation, "state.json");
        return File.Exists(own)
            ? [own]
            : SourceFiles.Directories(conversation)
                .Where(session => !Path.GetFileName(session).StartsWith(TitleHelper, StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .Select(session => Path.Combine(session, "state.json"));
    }

    private static string? Read(string state, string property)
    {
        try
        {
            using var stream = new FileStream(state, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                && value.GetString() is { Length: > 0 } text ? text : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>The app's markers around a request, like &lt;meta awareness="low" timestamp="…" /&gt;, and runs of white space.</summary>
    [GeneratedRegex(@"<[a-z][\w-]*\b[^>]*/?>|\s+", RegexOptions.IgnoreCase)]
    private static partial Regex Tags();
}
