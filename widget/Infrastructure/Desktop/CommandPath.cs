using System.IO;

namespace MiniInfos.Infrastructure.Desktop;

/// <summary>A command as a terminal finds it: the first folder on PATH holding it as an .exe, or as the .cmd npm lays.</summary>
public static class CommandPath
{
    private static readonly string[] Extensions = [".exe", ".cmd"];

    /// <summary>
    /// The PATH a new terminal would get: this process's, then the user's and the machine's as saved, so a
    /// command installed after MiniInfos started still counts.
    /// </summary>
    public static string Current() => OperatingSystem.IsWindows() ?
        string.Join(';', new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine }
            .Select(target => Environment.GetEnvironmentVariable("PATH", target)))
        : string.Join(Path.PathSeparator, UnixFolders());

    private static IEnumerable<string> UnixFolders()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Concat(new[] { "/opt/homebrew/bin", "/usr/local/bin", "/usr/bin", "/bin",
                Path.Combine(home, ".local", "bin"), Path.Combine(home, ".cargo", "bin"),
                Path.Combine(home, ".bun", "bin"), Path.Combine(home, ".volta", "bin") })
            .Concat(MiniInfos.Infrastructure.Storage.SourceFiles.Directories(Path.Combine(home, ".nvm", "versions", "node"))
                .OrderByDescending(folder => Version.TryParse(Path.GetFileName(folder).TrimStart('v'), out var version) ? version : new Version(0, 0))
                .Select(folder => Path.Combine(folder, "bin")))
            .Distinct(StringComparer.Ordinal);
    }

    /// <param name="path">A PATH value: folders separated by ';'.</param>
    /// <param name="command">The name typed in a terminal, without its extension.</param>
    public static string? Find(string? path, string command)
    {
        foreach (var folder in (path ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var extension in OperatingSystem.IsWindows() ? Extensions : [""])
            {
                var candidate = TryCombine(folder.Trim('"'), command + extension);
                if (candidate is not null && IsExecutable(candidate)) return candidate;
            }
        }

        return null;
    }

    public static bool IsExecutable(string path)
    {
        if (!File.Exists(path)) return false;
        if (OperatingSystem.IsWindows()) return true;
        try { return (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
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
