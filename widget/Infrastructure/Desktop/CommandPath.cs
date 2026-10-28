using System.IO;

namespace MiniInfos.Infrastructure.Windows;

/// <summary>A command as a terminal finds it: the first folder on PATH holding it as an .exe, or as the .cmd npm lays.</summary>
public static class CommandPath
{
    private static readonly string[] Extensions = [".exe", ".cmd"];

    /// <summary>
    /// The PATH a new terminal would get: this process's, then the user's and the machine's as saved, so a
    /// command installed after MiniInfos started still counts.
    /// </summary>
    public static string Current() =>
        string.Join(';', new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine }
            .Select(target => Environment.GetEnvironmentVariable("PATH", target)));

    /// <param name="path">A PATH value: folders separated by ';'.</param>
    /// <param name="command">The name typed in a terminal, without its extension.</param>
    public static string? Find(string? path, string command)
    {
        foreach (var folder in (path ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var extension in Extensions)
            {
                var candidate = TryCombine(folder.Trim('"'), command + extension);
                if (candidate is not null && File.Exists(candidate)) return candidate;
            }
        }

        return null;
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
