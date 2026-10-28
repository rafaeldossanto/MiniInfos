using System.IO;
using MiniInfos.Infrastructure.Desktop;

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
        Bundles = HostPaths.AppBundles("Claude"),
        Commands = ["claude"],
        EditorExtensions = ["anthropic.claude-code"],
        Files =
        [
            // The native installer's copy, on PATH only in terminals opened after it.
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", OperatingSystem.IsWindows() ? "claude.exe" : "claude"),
            // The desktop app as installed before the Store package.
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AnthropicClaude", "claude.exe"),
        ],
        Processes = ["claude"],
    };

    public static bool IsPresent() => ClaudeMod.Default.InstalledCode is not null
        || InstalledApps.Has(Footprint, InstallContext.Current());
}
