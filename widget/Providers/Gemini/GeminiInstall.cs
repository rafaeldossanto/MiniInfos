using MiniInfos.Infrastructure.Windows;

namespace MiniInfos.Providers.Gemini;

/// <summary>
/// Where an install of Gemini CLI shows: its gemini command (npm puts it on PATH). It runs on Node, so no process
/// carries its name, and the .gemini folder is no sign: it stays after an uninstall.
/// </summary>
public static class GeminiInstall
{
    public static AppFootprint Footprint { get; } = new()
    {
        Commands = ["gemini"],
    };

    public static bool IsPresent() => InstalledApps.Has(Footprint, InstallContext.Current());
}
