using System.IO;

namespace MiniInfos;

public static class ClaudePaths
{
    public static string ConfigRoot { get; } =
        Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

    public static string Projects { get; } = Path.Combine(ConfigRoot, "projects");

    public static string Feed { get; } = Path.Combine(ConfigRoot, "miniinfos", "sessions");

    public static string AppData { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiniInfos");
}
