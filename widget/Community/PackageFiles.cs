using System.IO;
using System.Text.RegularExpressions;

namespace MiniInfos.Community;

/// <summary>All package file access stays inside its own folder, including manually copied packages.</summary>
public static class PackageFiles
{
    public static string Relative(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new PackageException("Um caminho de arquivo está vazio.");
        path = path.Replace('\\', '/');
        var parts = path.Split('/');
        if (path.Length > 240 || parts.Length > 8 || parts.Any(part => part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ')
            || part.IndexOfAny([':', '\0', '<', '>', '"', '|', '?', '*']) >= 0
            || Regex.IsMatch(part, "^(con|prn|aux|nul|com[1-9]|lpt[1-9]|conin\\$|conout\\$)(?:\\.|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
            throw new PackageException($"Caminho de arquivo inválido: {path}");
        if (!new[] { ".json", ".png", ".md", ".txt" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            throw new PackageException("O pacote aceita somente JSON, PNG, Markdown e texto.");
        return path;
    }

    public static string Resolve(string root, string relative)
    {
        relative = Relative(relative);
        root = Path.GetFullPath(root);
        var path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new PackageException("Um arquivo aponta para fora do pacote.");
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new PackageException("Pacotes não aceitam links para outros arquivos ou pastas.");
            if (current.Equals(root, StringComparison.OrdinalIgnoreCase)) break;
        }
        return path;
    }

    public static IEnumerable<string> Enumerate(string root)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(root))
        {
            if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                throw new PackageException("Pacotes não aceitam links para outros arquivos ou pastas.");
            if (Directory.Exists(entry))
            {
                foreach (var file in Enumerate(entry)) yield return file;
            }
            else yield return entry;
        }
    }
}
