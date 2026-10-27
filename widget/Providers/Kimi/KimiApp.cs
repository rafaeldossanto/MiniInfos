using MiniInfos.Infrastructure.Windows;

namespace MiniInfos.Providers.Kimi;

/// <summary>Whether the Kimi desktop app has its window open. Kimi Code in a terminal has no window of its own.</summary>
public static class KimiApp
{
    public static bool IsOpen() => DesktopApps.HasWindow("Kimi", _ => true);
}
