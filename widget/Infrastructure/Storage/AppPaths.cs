using System.IO;

namespace MiniInfos.Infrastructure.Storage;

/// <summary>Where MiniInfos keeps its own files: the token ledgers and the card's settings.</summary>
public static class AppPaths
{
    /// <summary>%LOCALAPPDATA%\MiniInfos, or MINIINFOS_DATA_DIR when set (isolated runs, such as the checks).</summary>
    public static string Data { get; } =
        Environment.GetEnvironmentVariable("MINIINFOS_DATA_DIR")
        ?? Path.Combine(MiniInfos.Infrastructure.Desktop.HostPaths.Data, "MiniInfos");

    /// <summary>The Windows startup folder, or MINIINFOS_STARTUP_DIR when set (the checks, which must leave the real one alone).</summary>
    public static string Startup { get; } =
        Environment.GetEnvironmentVariable("MINIINFOS_STARTUP_DIR")
        ?? Environment.GetFolderPath(Environment.SpecialFolder.Startup);
}
