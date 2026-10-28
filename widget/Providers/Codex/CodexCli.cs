using System.Diagnostics;
using System.IO;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Infrastructure.Desktop;

namespace MiniInfos.Providers.Codex;

/// <summary>
/// Where the codex command lives, so MiniInfos can start its app-server: on PATH (a native
/// install, or the codex.cmd npm lays), else the copy the Codex desktop app keeps for itself,
/// which no PATH names (the Store app's included).
/// </summary>
public static class CodexCli
{
    private const string AppServerArguments = "app-server --listen stdio://";

    /// <summary>How to start the app-server, or null when no codex is installed.</summary>
    public static ProcessStartInfo? AppServer()
    {
        var codex = Find(
            CommandPath.Current(),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            DesktopApps.ExecutablePaths(true, "codex").Where(path => !OperatingSystem.IsWindows() || !DesktopApps.HasWindow("codex", window =>
                string.Equals(path, window, StringComparison.OrdinalIgnoreCase))));
        if (!OperatingSystem.IsWindows()) codex ??= HostPaths.AppBundles("Codex")
            .Select(bundle => Path.Combine(bundle, "Contents", "Resources", "codex")).FirstOrDefault(CommandPath.IsExecutable);
        if (codex is null) return null;

        var start = CommandRunner.StartInfo(codex, AppServerArguments);
        start.Environment["CODEX_HOME"] = CodexPaths.Root;
        start.Environment["PATH"] = CommandPath.Current();
        return start;
    }

    /// <summary>The codex to run: the first on <paramref name="path"/>, else the desktop app's newest copy.</summary>
    /// <param name="path">A PATH value: folders separated by ';'.</param>
    /// <param name="localAppData">The %LOCALAPPDATA% the desktop app keeps its copy under.</param>
    public static string? Find(string? path, string localAppData, IEnumerable<string>? runningExecutables = null) =>
        CommandPath.Find(path, "codex") ?? runningExecutables?.FirstOrDefault(candidate => File.Exists(candidate)
            && Path.GetFileName(candidate).Equals(OperatingSystem.IsWindows() ? "codex.exe" : "codex", StringComparison.OrdinalIgnoreCase)
            && (OperatingSystem.IsWindows() || !candidate.Contains(".app/Contents/MacOS/", StringComparison.OrdinalIgnoreCase))
            && CommandPath.IsExecutable(candidate)) ?? (OperatingSystem.IsWindows() ? DesktopCopy(localAppData) : null);

    /// <summary>
    /// The Codex desktop app keeps codex at %LOCALAPPDATA%\OpenAI\Codex\bin\&lt;build&gt;\codex.exe; a
    /// Store install may keep it in its package's own folder instead. The newest build wins.
    /// </summary>
    private static string? DesktopCopy(string localAppData)
    {
        var roots = new List<string> { Path.Combine(localAppData, "OpenAI", "Codex", "bin") };
        var packages = Path.Combine(localAppData, "Packages");
        roots.AddRange(SourceFiles.Directories(packages, "OpenAI.Codex_*")
                .Select(package => Path.Combine(package, "LocalCache", "Local", "OpenAI", "Codex", "bin")));

        return roots
            .Where(Directory.Exists)
            .SelectMany(root => SourceFiles.Directories(root))
            .Select(build => new FileInfo(Path.Combine(build, "codex.exe")))
            .Where(file => file.Exists)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => file.FullName)
            .FirstOrDefault();
    }
}
