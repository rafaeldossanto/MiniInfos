using System.IO;

namespace MiniInfos.Infrastructure.Desktop;

/// <summary>Native per-user locations. No root-drive scan or Windows folder assumptions on Unix.</summary>
public static class HostPaths
{
    public static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public static string Configuration => OperatingSystem.IsMacOS() ? Path.Combine(Home, "Library", "Application Support")
        : OperatingSystem.IsLinux() ? AbsoluteOr("XDG_CONFIG_HOME", Path.Combine(Home, ".config"))
        : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    public static string Data => OperatingSystem.IsMacOS() ? Configuration
        : OperatingSystem.IsLinux() ? AbsoluteOr("XDG_DATA_HOME", Path.Combine(Home, ".local", "share"))
        : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static string[] AppBundles(string name) => OperatingSystem.IsMacOS()
        ? [Path.Combine("/Applications", name + ".app"), Path.Combine(Home, "Applications", name + ".app")] : [];
    private static string AbsoluteOr(string variable, string fallback) => Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value && Path.IsPathRooted(value) ? value : fallback;
}
