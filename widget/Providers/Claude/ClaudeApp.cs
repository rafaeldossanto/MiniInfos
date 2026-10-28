using MiniInfos.Infrastructure.Desktop;

namespace MiniInfos.Providers.Claude;

/// <summary>
/// Whether the Claude desktop app has its window open. Its process and the Claude Code chats it
/// starts share the name claude.exe; only the app has a window, and the chats run from a
/// claude-code folder.
/// </summary>
public static class ClaudeApp
{
    private const string CodeFolder = @"\claude-code\";

    /// <summary>A path Windows keeps from us still counts: the Store app's window is enough.</summary>
    public static bool IsOpen() =>
        DesktopApps.HasWindow("claude", path => path is null || !path.Replace('/', '\\').Contains(CodeFolder, StringComparison.OrdinalIgnoreCase));
}
