using MiniInfos.Infrastructure.Windows;

namespace MiniInfos.Providers.Codex;

/// <summary>
/// Whether the Codex desktop app has its window open. The Store app runs as ChatGPT.exe, the
/// same name as the ChatGPT app: only one running from the Codex package counts. The CLI has no
/// window of its own.
/// </summary>
public static class CodexApp
{
    private const string StorePackage = @"\OpenAI.Codex_";

    public static bool IsOpen() =>
        DesktopApps.HasWindow("Codex", _ => true)
        || DesktopApps.HasWindow("ChatGPT", path => path?.Contains(StorePackage, StringComparison.OrdinalIgnoreCase) == true);
}
