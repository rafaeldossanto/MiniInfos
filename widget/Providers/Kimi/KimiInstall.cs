using System.IO;
using MiniInfos.Infrastructure.Desktop;

namespace MiniInfos.Providers.Kimi;

/// <summary>
/// Where an install of Kimi shows: the desktop app (its own installer, with a Store package for sharing to it)
/// or Kimi Code's command. The kimi-desktop data folder is no sign: it stays after an uninstall.
/// </summary>
public static class KimiInstall
{
    public static AppFootprint Footprint { get; } = new()
    {
        StorePackages = ["KimiShareTarget_m564vt17ty7ce"],
        Bundles = HostPaths.AppBundles("Kimi"),
        Commands = ["kimi"],
        Files = [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Kimi", "Kimi.exe")],
        Processes = ["Kimi"],
    };

    public static bool IsPresent() => InstalledApps.Has(Footprint, InstallContext.Current());
}
