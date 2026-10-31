using System.IO;
using MiniInfos.Infrastructure.Desktop;

namespace MiniInfos.Providers.Antigravity;

public static class AntigravityInstall
{
    public static AppFootprint Footprint { get; } = new()
    {
        Commands = ["antigravity", "agy"],
        Bundles = HostPaths.AppBundles("Antigravity"),
        Files = [Path.Combine(HostPaths.Data, "Programs", "antigravity", "Antigravity.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Antigravity", "Antigravity.exe")],
        Processes = ["Antigravity"],
    };

    public static bool IsPresent() => InstalledApps.Has(Footprint, InstallContext.Current());
    public static bool IsOpen() => DesktopApps.HasWindow("Antigravity", _ => true);
}
