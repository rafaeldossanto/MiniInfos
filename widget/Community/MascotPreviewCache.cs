using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media.Imaging;
using MiniInfos.Mascot;

namespace MiniInfos.Community;

/// <summary>Versioned PNGs live outside packages; settings never decode installed sprite sheets on their UI thread.</summary>
public sealed class MascotPreviewCache(string root, Func<InstalledPackage, BitmapSource>? generate = null)
{
    public string Root { get; } = Path.GetFullPath(root);
    private readonly ConcurrentDictionary<string, Lazy<Task<BitmapSource?>>> _previews = new();
    private static readonly ConcurrentDictionary<IPetAppearance, Lazy<Task<BitmapSource>>> BuiltIns = new();

    public Task<BitmapSource?> GetAsync(InstalledPackage? package)
    {
        if (package is null) return GetBuiltInAsync(null);
        var key = Location(package);
        return _previews.GetOrAdd(key, _ => new(() => Task.Run(() => ReadOrCreate(package, key)))).Value;
    }

    public async Task<BitmapSource?> GetBuiltInAsync(string? id)
    {
        var appearance = PetAppearances.BuiltIn(id) ?? throw new PackageException("Mascote padrão não encontrado.");
        return await BuiltIns.GetOrAdd(appearance, pet => new(() => Task.Run(() =>
            PackagePreviews.Mascot(pet.Frame(PetState.Idle, 0))))).Value.ConfigureAwait(false);
    }

    /// <summary>Called after a successful install, also replacing a stale preview for a same-version reimport.</summary>
    public void Refresh(InstalledPackage package)
    {
        var path = Location(package);
        var image = TryCreate(package, path);
        _previews[path] = new(() => Task.FromResult(image));
    }

    public void Remove(InstalledPackage package)
    {
        var path = Location(package);
        _previews.TryRemove(path, out _);
        try { File.Delete(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    private BitmapSource? ReadOrCreate(InstalledPackage package, string path)
    {
        try
        {
            if (File.Exists(path) && PackageImages.Dimensions(path) == (PackagePreviews.Size, PackagePreviews.Size))
                return PackageImages.Load(path);
        }
        catch (Exception e) when (PreviewError(e)) { }
        return TryCreate(package, path);
    }

    private BitmapSource? TryCreate(InstalledPackage package, string path)
    {
        BitmapSource image;
        try { image = (generate ?? PackagePreviews.Mascot)(package); }
        catch (Exception e) when (PreviewError(e)) { return null; }
        try
        {
            Directory.CreateDirectory(Root);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, PackageImages.Encode(image));
                File.Move(temporary, path, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        catch (Exception e) when (PreviewError(e)) { }
        return image;
    }

    private string Location(InstalledPackage package)
    {
        PackageValidation.Id(package.Id);
        if (package.Manifest.Kind != PackageKind.Mascot || !Version.TryParse(package.Manifest.Version, out _))
            throw new PackageException("Prévia de mascote inválida.");
        return Path.Combine(Root, $"{package.Id}-{package.Manifest.Version}.png");
    }

    private static bool PreviewError(Exception e) => e is PackageException or IOException or UnauthorizedAccessException;
}
