using System.IO;

namespace MiniInfos.Providers.Gemini;

/// <summary>
/// Where Gemini CLI records its chats: ~/.gemini/tmp/&lt;project&gt;/chats/session-*.jsonl, one folder per project,
/// with subagents' chats nested under their session. The Gemini web app keeps nothing on this PC.
/// </summary>
public static class GeminiPaths
{
    public static string Home => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini");

    public static IReadOnlyList<string> ChatFolders => Directory.Exists(Path.Combine(Home, "tmp")) ? [Path.Combine(Home, "tmp")] : [];
}
