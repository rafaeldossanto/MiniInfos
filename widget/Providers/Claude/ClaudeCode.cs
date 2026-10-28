using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using MiniInfos.Infrastructure.Desktop;

namespace MiniInfos.Providers.Claude;

/// <summary>
/// Finds Claude Code wherever it is installed, so the miniinfos mod can be registered with it: the one on PATH first,
/// then every file of the user's folder, where the per-user installs land, and then every file of every local drive,
/// hidden folders included, without walking a folder twice. Nothing found is run to tell what it is: Claude
/// Code's executable names itself in its version info (the desktop app's own claude.exe does not), and npm's
/// claude.cmd points at its package.
/// </summary>
public static class ClaudeCode
{
    private const string ProductName = "Claude Code";
    private const string Package = "claude-code";

    private static readonly string[] Names = OperatingSystem.IsWindows() ? ["claude.exe", "claude.cmd"] : ["claude"];

    /// <summary>Every Claude Code found, PATH's first and then the roots' as the scan reaches them, each once.</summary>
    /// <param name="roots">Where to scan, in order, a root already walked left out of the later ones: the user's folder and then every ready local drive when null.</param>
    /// <param name="path">The PATH to look in first: a new terminal's when null.</param>
    public static IEnumerable<string> Find(IEnumerable<string>? roots = null, string? path = null)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (CommandPath.Find(path ?? CommandPath.Current(), "claude") is { } command && IsClaudeCode(command) && seen.Add(Path.GetFullPath(command)))
            yield return command;

        var walked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots ?? [Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), .. Drives()])
        {
            foreach (var file in Files(root, walked))
            {
                if (IsClaudeCode(file) && seen.Add(Path.GetFullPath(file))) yield return file;
            }

            walked.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)));
        }
    }

    /// <summary>Whether a file named like Claude Code is it: an executable that says so, or a script that starts its package.</summary>
    public static bool IsClaudeCode(string path)
    {
        try
        {
            if (Path.GetExtension(path).Equals(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(path);
                var buffer = new char[8192];
                var script = new string(buffer, 0, reader.Read(buffer, 0, buffer.Length));
                return script.Contains("@anthropic-ai", StringComparison.OrdinalIgnoreCase) && script.Contains(Package, StringComparison.OrdinalIgnoreCase);
            }

            if (!OperatingSystem.IsWindows()) return CommandPath.IsExecutable(path);

            var info = FileVersionInfo.GetVersionInfo(path);
            return string.Equals(info.ProductName, ProductName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(info.FileDescription, ProductName, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static IEnumerable<string> Drives()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            string? root = null;
            try
            {
                if (drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Removable) root = drive.RootDirectory.FullName;
            }
            catch (IOException)
            {
            }

            if (root is not null) yield return root;
        }
    }

    /// <summary>
    /// Every file under the root with a name Claude Code goes by, the folders in <paramref name="walked"/> left out.
    /// Hidden and system folders are walked too; folder links are not, since they lead back into the tree (AppData's
    /// old "Application Data" link loops on itself).
    /// </summary>
    private static IEnumerable<string> Files(string root, IReadOnlySet<string> walked)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };
        IEnumerator<string> files;
        try
        {
            files = new FileSystemEnumerable<string>(root, (ref FileSystemEntry entry) => entry.ToFullPath(), options)
            {
                ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory && HasClaudeName(entry.FileName),
                ShouldRecursePredicate = (ref FileSystemEntry entry) => !walked.Contains(entry.ToFullPath()),
            }.GetEnumerator();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            yield break;
        }

        using (files)
        {
            while (true)
            {
                string file;
                try
                {
                    if (!files.MoveNext()) yield break;
                    file = files.Current;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // A drive pulled out or a folder gone mid-walk: what was found so far still counts.
                    yield break;
                }

                yield return file;
            }
        }
    }

    private static bool HasClaudeName(ReadOnlySpan<char> fileName)
    {
        foreach (var name in Names)
        {
            if (fileName.Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}
