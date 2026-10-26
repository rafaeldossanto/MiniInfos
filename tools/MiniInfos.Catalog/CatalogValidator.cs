using System.IO;
using MiniInfos.Community;
using MiniInfos.Community.Catalog;

namespace MiniInfos.Catalog;

public sealed class CatalogValidator
{
    public InstalledPackage Validate(string folder)
    {
        using var package = Prepare(folder);
        return package.Source;
    }

    internal PreparedPackage Prepare(string folder)
    {
        folder = Path.GetFullPath(folder);
        var source = PackageLibrary.ReadSource(folder, decodeImages: true);
        if (Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)) != source.Id)
            throw new PackageException($"A pasta do pacote precisa ter o mesmo nome do ID: {source.Id}.");
        if (source.Manifest.License.Trim().Equals("UNLICENSED", StringComparison.OrdinalIgnoreCase))
            throw new PackageException("Escolha uma licença antes de publicar; UNLICENSED é apenas um marcador.");
        var files = PackageFiles.Enumerate(folder).Take(129).Select(path => new FileInfo(path)).ToArray();
        if (files.Length > 128 || files.Any(file => file.Length > 32 * 1024 * 1024) || files.Sum(file => file.Length) > 64 * 1024 * 1024)
            throw new PackageException("O pacote-fonte excede os limites de arquivos do MiniInfos.");
        var assets = files.ToDictionary(file => PackageFiles.Relative(Path.GetRelativePath(folder, file.FullName).Replace('\\', '/')),
            file => File.ReadAllBytes(file.FullName), StringComparer.OrdinalIgnoreCase);
        var archive = Path.GetTempFileName();
        try
        {
            PackageLibrary.Create(archive, source.Manifest, assets);
            if (new FileInfo(archive).Length > CatalogIndex.MaxPackageBytes)
                throw new PackageException($"O pacote {source.Id} excede 8 MB compactado, limite do catálogo.");
            return new(source, archive);
        }
        catch { File.Delete(archive); throw; }
    }
}

internal sealed record PreparedPackage(InstalledPackage Source, string Archive) : IDisposable
{
    public void Dispose() => File.Delete(Archive);
}
