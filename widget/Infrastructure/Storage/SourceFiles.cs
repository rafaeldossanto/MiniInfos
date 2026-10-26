using System.IO;

namespace MiniInfos.Infrastructure.Storage;

/// <summary>Small, defensive filesystem operations shared by source discovery and log readers.</summary>
public static class SourceFiles
{
    public static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            if (path == "~" || path.StartsWith("~/") || path.StartsWith("~\\"))
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.Length > 1 ? path[2..] : "");
            if (!Path.IsPathFullyQualified(path)) return null;
            path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            if (Directory.Exists(path) && new DirectoryInfo(path).ResolveLinkTarget(returnFinalTarget: true) is { } target) path = target.FullName;
            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return null; }
    }

    public static IReadOnlyList<string> Distinct(IEnumerable<string?> paths) => paths.Select(Normalize)
        .OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static string[] Directories(string root, string pattern = "*")
    {
        try { return Directory.GetDirectories(root, pattern, new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System }); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return []; }
    }

    public static string[] Files(string root, string pattern, bool recursive = false) =>
        Array.ConvertAll(FileInfos(root, pattern, recursive), file => file.FullName);

    /// <summary>The files with the length the folder listing already carries: learning it opens nothing.</summary>
    public static FileInfo[] FileInfos(string root, string pattern, bool recursive = false)
    {
        try
        {
            return new DirectoryInfo(root).GetFiles(pattern, new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = recursive,
                AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return []; }
    }
}
