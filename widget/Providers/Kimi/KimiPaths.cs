using System.IO;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Sources;

namespace MiniInfos.Providers.Kimi;

/// <summary>
/// Where Kimi Code keeps its sessions: its own home (KIMI_CODE_HOME, else ~/.kimi-code) and the copy the
/// Kimi desktop app runs for its agent, under the app's data folder. Both share one layout:
/// sessions/wd_*/&lt;session&gt;/agents/&lt;agent&gt;/wire.jsonl.
/// </summary>
public static class KimiPaths
{
    public static string DesktopData => Path.Combine(MiniInfos.Infrastructure.Desktop.HostPaths.Configuration, "kimi-desktop");

    /// <summary>The desktop app's runtime: sessions under it are the app's agent, not the CLI.</summary>
    public static string DesktopHome => Path.Combine(DesktopData, "daimon-share", "daimon", "runtime", "kimi-code", "home");

    /// <summary>The desktop app's context meter per agent conversation.</summary>
    public static string DesktopContext => Path.Combine(DesktopData, "kimi-agent", "conversation-context-usage.json");

    public static IReadOnlyList<string> Homes => SourceFiles.Distinct(UsageSources.EnvironmentHomes("KIMI_CODE_HOME")
        .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kimi-code"))
        .Append(DesktopHome));

    public static IReadOnlyList<string> SessionFolders => Homes.Select(home => Path.Combine(home, "sessions")).Where(Directory.Exists).ToArray();

    public static bool IsDesktop(string path) => path.StartsWith(DesktopHome, StringComparison.OrdinalIgnoreCase);
}
