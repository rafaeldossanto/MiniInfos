using System.IO;
using System.IO.Compression;
using System.Text.Json;
using MiniInfos.Infrastructure.Storage;

namespace MiniInfos.Community;

/// <summary>Installs data-only packages atomically. The library never loads community code or XAML.</summary>
public sealed class PackageLibrary(string root)
{
    private const long MaxBytes = 64 * 1024 * 1024;
    private const int MaxFiles = 128;
    public static PackageLibrary Shared { get; } = new(Path.Combine(AppPaths.Data, "packages"));
    public string Root { get; } = Path.GetFullPath(root);
    private readonly Lock _gate = new();

    public IReadOnlyList<InstalledPackage> List(PackageKind kind)
    {
        lock (_gate)
        {
            string folder;
            try { folder = KindFolder(kind); }
            catch (Exception e) when (e is PackageException or IOException or UnauthorizedAccessException) { return []; }
            if (!Directory.Exists(folder)) return [];
            var packages = new List<InstalledPackage>();
            foreach (var directory in SourceFiles.Directories(folder))
            {
                try
                {
                    var package = Read(directory);
                    if (package.Manifest.Kind == kind && Path.GetFileName(directory) == package.Id) packages.Add(package);
                }
                catch (Exception e) when (e is PackageException or IOException or UnauthorizedAccessException) { }
            }
            return packages.OrderBy(package => package.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }
    }

    public InstalledPackage? Find(PackageKind kind, string? id)
    {
        if (id is null) return null;
        lock (_gate)
        {
            try
            {
                PackageValidation.Id(id);
                var folder = Location(kind, id);
                if (!Directory.Exists(folder)) return null;
                var package = Read(folder);
                return package.Id == id && package.Manifest.Kind == kind ? package : null;
            }
            catch (Exception e) when (e is PackageException or IOException or UnauthorizedAccessException) { return null; }
        }
    }

    public InstalledPackage Install(string archive, PackageKind expectedKind)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Root);
            var staging = Owned(Path.Combine(Root, ".install-" + Guid.NewGuid().ToString("N")));
            var backup = Owned(Path.Combine(Root, ".previous-" + Guid.NewGuid().ToString("N")));
            var committed = false;
            Directory.CreateDirectory(staging);
            try
            {
                Extract(archive, staging);
                var package = Read(staging, decodeImages: true);
                if (package.Manifest.Kind != expectedKind) throw new PackageException(expectedKind == PackageKind.Theme
                    ? "Este é um pacote de mascote. Adicione-o na página Mascote." : "Este é um pacote de tema. Adicione-o na página Aparência.");
                var destination = Location(expectedKind, package.Id);
                Directory.CreateDirectory(KindFolder(expectedKind));
                if (Directory.Exists(destination))
                {
                    _ = PackageFiles.Enumerate(destination).ToArray();
                    Directory.Move(Owned(destination), backup);
                }
                try { Directory.Move(staging, Owned(destination)); }
                catch
                {
                    if (Directory.Exists(backup)) Directory.Move(backup, Owned(destination));
                    throw;
                }
                committed = true;
                return new(package.Manifest, destination);
            }
            catch (Exception e) when (e is InvalidDataException or JsonException)
            { throw new PackageException("Não foi possível ler o pacote. Confira o ZIP e seu manifest.json.", e); }
            finally
            {
                DeleteOwned(staging);
                if (committed) DeleteOwned(backup);
            }
        }
    }

    public void Remove(PackageKind kind, string id)
    {
        lock (_gate) { PackageValidation.Id(id); DeleteOwned(Location(kind, id)); }
    }

    public void Export(PackageKind kind, string id, string output)
    {
        lock (_gate)
        {
            if (Path.GetFullPath(output).StartsWith(Path.TrimEndingDirectorySeparator(Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new PackageException("Escolha uma pasta de destino fora da biblioteca de pacotes instalados.");
            var package = Find(kind, id) ?? throw new PackageException("O pacote não está mais instalado.");
            var sources = PackageFiles.Enumerate(package.Directory).Take(MaxFiles + 1).Select(file => new FileInfo(file)).ToArray();
            if (sources.Length > MaxFiles || sources.Any(file => file.Length > 32 * 1024 * 1024) || sources.Sum(file => file.Length) > MaxBytes)
                throw new PackageException("O pacote instalado excede os limites de exportação.");
            var files = sources.Select(info => info.FullName).Select(file =>
                (Name: Path.GetRelativePath(package.Directory, file).Replace('\\', '/'), Bytes: File.ReadAllBytes(file))).ToArray();
            WriteArchive(output, files.ToDictionary(file => file.Name, file => file.Bytes, StringComparer.OrdinalIgnoreCase));
        }
    }

    public static void Create(string output, PackageManifest manifest, IReadOnlyDictionary<string, byte[]> assets)
    {
        var files = new Dictionary<string, byte[]>(assets, StringComparer.OrdinalIgnoreCase)
        {
            ["manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(manifest, PackageManifest.Json),
        };
        WriteArchive(output, files);
    }

    private static void WriteArchive(string output, IReadOnlyDictionary<string, byte[]> files)
    {
        var temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
                foreach (var (name, bytes) in files)
                {
                    var entry = zip.CreateEntry(PackageFiles.Relative(name), CompressionLevel.Optimal);
                    using var stream = entry.Open();
                    stream.Write(bytes);
                }
            File.Move(temporary, output, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void Extract(string archive, string staging)
    {
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count > MaxFiles) throw new PackageException("O pacote excede o limite de 128 arquivos.");
        long total = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) continue;
            var relative = PackageFiles.Relative(entry.FullName);
            if (!seen.Add(relative)) throw new PackageException("O pacote contém caminhos duplicados.");
            if (entry.Length < 0 || entry.Length > 32 * 1024 * 1024 || entry.Length > MaxBytes - total)
                throw new PackageException("O pacote excede o limite de 64 MB descompactados.");
            total += entry.Length;
            var path = PackageFiles.Resolve(staging, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var input = entry.Open();
            using var output = File.Create(path);
            var buffer = new byte[81920];
            long written = 0;
            int read;
            while ((read = input.Read(buffer)) > 0)
            {
                written += read;
                if (written > entry.Length) throw new PackageException("Um arquivo excedeu o tamanho declarado pelo pacote.");
                output.Write(buffer, 0, read);
            }
        }
    }

    private static InstalledPackage Read(string folder, bool decodeImages = false)
    {
        try
        {
            var path = PackageFiles.Resolve(folder, "manifest.json");
            if (!File.Exists(path)) throw new PackageException("O pacote precisa conter manifest.json na raiz do ZIP.");
            if (new FileInfo(path).Length > 128 * 1024) throw new PackageException("O manifest.json excede 128 KB.");
            var manifest = JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(path), PackageManifest.Json)
                ?? throw new PackageException("O manifest.json está vazio.");
            PackageValidation.Validate(manifest, folder, decodeImages);
            return new(manifest, folder);
        }
        catch (JsonException e) { throw new PackageException("O manifest.json não segue o formato de pacotes do MiniInfos.", e); }
    }

    private string KindFolder(PackageKind kind) => Owned(Path.Combine(Root, kind == PackageKind.Theme ? "themes" : "mascots"));
    private string Location(PackageKind kind, string id) => Owned(Path.Combine(KindFolder(kind), id));
    private string Owned(string path)
    {
        path = Path.GetFullPath(path);
        if (!path.StartsWith(Path.TrimEndingDirectorySeparator(Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new PackageException("O caminho está fora da biblioteca de pacotes.");
        // Reject redirected folders before reading, replacing or removing a package.
        var check = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        while (check is not null)
        {
            if (Directory.Exists(check) && (File.GetAttributes(check) & FileAttributes.ReparsePoint) != 0)
                throw new PackageException("A biblioteca de pacotes contém um link para outra pasta.");
            if (check.Equals(Root, StringComparison.OrdinalIgnoreCase)) break;
            check = Path.GetDirectoryName(check);
        }
        return path;
    }

    private void DeleteOwned(string path)
    {
        path = Owned(path); // Exact absolute target verified under Root before any recursive removal.
        if (!Directory.Exists(path)) return;
        _ = PackageFiles.Enumerate(path).ToArray();
        Directory.Delete(path, recursive: true);
    }
}
