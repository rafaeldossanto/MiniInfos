using System.Runtime.InteropServices;

namespace MiniInfos.Updates;

public static class ReleaseAssets
{
    public static string Current => For(OperatingSystem.IsMacOS() ? "macos" : OperatingSystem.IsLinux() ? "linux" : "windows", RuntimeInformation.ProcessArchitecture);
    public static string For(string platform, Architecture architecture) => platform switch
    {
        "windows" => GitHubReleases.InstallerName,
        "macos" when architecture is Architecture.Arm64 or Architecture.X64 => $"MiniInfos-macos-{(architecture == Architecture.Arm64 ? "arm64" : "x64")}.dmg",
        "linux" when architecture is Architecture.Arm64 or Architecture.X64 => $"MiniInfos-linux-{(architecture == Architecture.Arm64 ? "arm64" : "x64")}.tar.gz",
        _ => throw new PlatformNotSupportedException("Unsupported MiniInfos platform or architecture."),
    };
}
