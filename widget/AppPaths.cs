using System.IO;

namespace MiniInfos;

/// <summary>Where MiniInfos keeps its own files: the token ledgers and the card's settings.</summary>
public static class AppPaths
{
    /// <summary>%LOCALAPPDATA%\MiniInfos, or MINIINFOS_DATA_DIR when set (isolated runs, such as the checks).</summary>
    public static string Data { get; } =
        Environment.GetEnvironmentVariable("MINIINFOS_DATA_DIR")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiniInfos");
}

/// <summary>What MiniInfos says of itself to other programs.</summary>
public static class AppInfo
{
    /// <summary>The version the project file sets: the one the installer is built with too.</summary>
    public static string Version { get; } = typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
