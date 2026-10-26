using System.IO;
using System.Security.Cryptography;
using MiniInfos.Community;
using MiniInfos.Community.Catalog;

namespace MiniInfos.Catalog;

public sealed class CatalogBuilder(Func<DateTimeOffset>? clock = null)
{
    public CatalogIndex Build(string packagesFolder, string output, CatalogIndex? previous = null)
    {
        packagesFolder = Path.GetFullPath(packagesFolder);
        output = Path.GetFullPath(output);
        if (output.Equals(packagesFolder, StringComparison.OrdinalIgnoreCase)
            || output.StartsWith(Path.TrimEndingDirectorySeparator(packagesFolder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new PackageException("A pasta de saída precisa ficar fora da pasta dos pacotes-fonte.");
        previous?.Validate();
        var prepared = new List<PreparedPackage>();
        try
        {
            var validator = new CatalogValidator();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var folders = Directory.EnumerateDirectories(packagesFolder).Take(CatalogIndex.MaxEntries + 1).ToArray();
            if (folders.Length > CatalogIndex.MaxEntries) throw new PackageException("O catálogo aceita até 2000 pacotes.");
            foreach (var folder in folders.Order(StringComparer.Ordinal))
            {
                var item = validator.Prepare(folder);
                prepared.Add(item);
                var manifest = item.Source.Manifest;
                if (!ids.Add(manifest.Id)) throw new PackageException($"ID duplicado: {manifest.Id}.");
                var old = previous?.Packages.SingleOrDefault(entry => entry.Id == manifest.Id);
                if (old is not null && PackageValidation.ParseVersion(manifest.Version) <= PackageValidation.ParseVersion(old.Version))
                    throw new PackageException($"A versão de {manifest.Id} precisa ser maior que {old.Version}.");
            }
            var entries = new List<CatalogEntry>();
            var previews = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var item in prepared)
            {
                var manifest = item.Source.Manifest;
                var prefix = $"{manifest.Id}-{manifest.Version}";
                var preview = PackageImages.Encode(manifest.Kind == PackageKind.Mascot
                    ? PackagePreviews.Mascot(item.Source) : PackagePreviews.Theme(item.Source));
                previews[prefix + ".png"] = preview;
                using var stream = File.OpenRead(item.Archive);
                entries.Add(new(manifest.Id, manifest.Kind, manifest.Name, manifest.Author, manifest.Version, manifest.License,
                    manifest.Description ?? "", prefix + ".miniinfos", stream.Length, Hash(SHA256.HashData(stream)),
                    prefix + ".png", Hash(SHA256.HashData(preview))));
            }
            var index = new CatalogIndex(1, (clock ?? (() => DateTimeOffset.UtcNow))(), entries.OrderBy(entry => entry.Id, StringComparer.Ordinal).ToArray());
            var json = index.Encode();
            Directory.CreateDirectory(output);
            foreach (var item in prepared)
                File.Copy(item.Archive, Path.Combine(output, $"{item.Source.Id}-{item.Source.Manifest.Version}.miniinfos"), overwrite: true);
            foreach (var (name, bytes) in previews) File.WriteAllBytes(Path.Combine(output, name), bytes);
            File.WriteAllBytes(Path.Combine(output, "index.json"), json);
            return index;
        }
        finally { foreach (var item in prepared) item.Dispose(); }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
