using System.IO;

namespace MiniInfos;

public static class CodexPaths
{
    public static string Root { get; } =
        Environment.GetEnvironmentVariable("CODEX_HOME")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");

    public static string Sessions { get; } = Path.Combine(Root, "sessions");
}
