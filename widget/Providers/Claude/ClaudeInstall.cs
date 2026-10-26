using System.IO;
using MiniInfos.Infrastructure.Windows;

namespace MiniInfos.Providers.Claude;

/// <summary>
/// Where an install of Claude shows: the desktop app (from the Store, or its older installer), Claude
/// Code's command or its editor extension. The Claude folder is no sign: it stays after an uninstall.
/// </summary>
public static class ClaudeInstall
{
    public static AppFootprint Footprint { get; } = new()
    {
        StorePackages = ["Claude_pzs8sxrjxfjjc"],
        Commands = ["claude"],
        EditorExtensions = ["anthropic.claude-code"],
        Files =
        [
            // The native installer's copy, on PATH only in terminals opened after it.
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "claude.exe"),
            // The desktop app as installed before the Store package.
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AnthropicClaude", "claude.exe"),
        ],
        Processes = ["claude"],
    };

    public static bool IsPresent() => InstalledApps.Has(Footprint, InstallContext.Current());
}
