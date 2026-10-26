using System.IO;

namespace MiniInfos;

public static class CodexPaths
{
    public static IReadOnlyList<string> Roots => UsageSources.Shared.Current.CodexRoots;
    public static IReadOnlyList<string> SessionFolders => UsageSources.Shared.Current.CodexSessions;

    public static string Root => UsageSources.EnvironmentHomes("CODEX_HOME").FirstOrDefault() ?? Roots.FirstOrDefault(root => File.Exists(Path.Combine(root, "auth.json"))
        || File.Exists(Path.Combine(root, "config.toml"))) ?? Roots[0];
    public static string Sessions => Path.Combine(Root, "sessions");
}
