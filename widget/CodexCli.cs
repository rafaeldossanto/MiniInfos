using System.Diagnostics;
using System.IO;

namespace MiniInfos;

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
            Environment.GetEnvironmentVariable("PATH"),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        if (codex is null) return null;

        // A batch file runs through cmd.exe: a process can't start one by itself.
        return Path.GetExtension(codex).Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            ? new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe", $"/d /s /c \"\"{codex}\" {AppServerArguments}\"")
            : new ProcessStartInfo(codex, AppServerArguments);
    }

    /// <summary>The codex to run: the first on <paramref name="path"/>, else the desktop app's newest copy.</summary>
    /// <param name="path">A PATH value: folders separated by ';'.</param>
    /// <param name="localAppData">The %LOCALAPPDATA% the desktop app keeps its copy under.</param>
    public static string? Find(string? path, string localAppData) => OnPath(path) ?? DesktopCopy(localAppData);

    private static string? OnPath(string? path)
    {
        foreach (var folder in (path ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var name in (string[])["codex.exe", "codex.cmd"])
            {
                var candidate = TryCombine(folder.Trim('"'), name);
                if (candidate is not null && File.Exists(candidate)) return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// The Codex desktop app keeps codex at %LOCALAPPDATA%\OpenAI\Codex\bin\&lt;build&gt;\codex.exe; a
    /// Store install may keep it in its package's own folder instead. The newest build wins.
    /// </summary>
    private static string? DesktopCopy(string localAppData)
    {
        var roots = new List<string> { Path.Combine(localAppData, "OpenAI", "Codex", "bin") };
        var packages = Path.Combine(localAppData, "Packages");
        if (Directory.Exists(packages))
        {
            roots.AddRange(Directory.EnumerateDirectories(packages, "OpenAI.Codex_*")
                .Select(package => Path.Combine(package, "LocalCache", "Local", "OpenAI", "Codex", "bin")));
        }

        return roots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateDirectories(root))
            .Select(build => new FileInfo(Path.Combine(build, "codex.exe")))
            .Where(file => file.Exists)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => file.FullName)
            .FirstOrDefault();
    }

    private static string? TryCombine(string folder, string name)
    {
        try
        {
            return Path.Combine(folder, name);
        }
        catch (ArgumentException)
        {
            // A PATH entry with characters no path can hold.
            return null;
        }
    }
}
