using System.IO;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using MiniInfos.Infrastructure.Network;
using MiniInfos.Infrastructure.Storage;

namespace MiniInfos.Community.Catalog;

public sealed record CachedCatalog(DateTimeOffset SavedAt, CatalogSnapshot Snapshot);

public sealed class CatalogCache(string root, Func<DateTimeOffset>? clock = null)
{
    public static readonly TimeSpan ValidFor = TimeSpan.FromHours(1);
    public const int MaxPreviewBytes = 512 * 1024;
    private readonly string _root = Path.GetFullPath(root);
    private readonly SemaphoreSlim _previews = new(4);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _previewLocks = new(StringComparer.OrdinalIgnoreCase);
    private JsonFile<CachedCatalog> FileCache => new(Path.Combine(_root, "index.json"));

    public CatalogSnapshot? Load()
    {
        var path = Path.Combine(_root, "index.json");
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 8 * 1024 * 1024) return null;
            var value = FileCache.Load();
            var age = Now - (value?.SavedAt ?? default);
            if (value?.Snapshot is not { Index: not null, Assets: not null } snapshot || age < TimeSpan.Zero || age >= ValidFor) return null;
            snapshot.Index.Encode();
            return snapshot;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or PackageException) { return null; }
    }

    public void Save(CatalogSnapshot snapshot) => FileCache.Save(new(Now, snapshot));
    private DateTimeOffset Now => (clock ?? (() => DateTimeOffset.UtcNow))();

    public async Task<BitmapSource?> PreviewAsync(CatalogSnapshot snapshot, CatalogEntry entry, HttpClient http, CancellationToken cancellation)
    {
        var gate = _previewLocks.GetOrAdd(entry.PreviewSha256, _ => new(1));
        await gate.WaitAsync(cancellation).ConfigureAwait(false);
        try { return await ReadPreviewAsync(snapshot, entry, http, cancellation).ConfigureAwait(false); }
        finally { gate.Release(); }
    }

    private async Task<BitmapSource?> ReadPreviewAsync(CatalogSnapshot snapshot, CatalogEntry entry, HttpClient http, CancellationToken cancellation)
    {
        await _previews.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            return await Task.Run(async () =>
            {
                var folder = Path.Combine(_root, "previews");
                var path = PackageFiles.Resolve(folder, entry.PreviewSha256 + ".png");
                byte[]? bytes = null;
                var age = Now - (File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path)) : default);
                if (File.Exists(path) && age >= TimeSpan.Zero && age < ValidFor && new FileInfo(path).Length <= MaxPreviewBytes)
                {
                    var existing = await File.ReadAllBytesAsync(path, cancellation).ConfigureAwait(false);
                    if (Convert.ToHexString(SHA256.HashData(existing)).Equals(entry.PreviewSha256, StringComparison.OrdinalIgnoreCase)) bytes = existing;
                }
                if (bytes is null)
                {
                    bytes = await new GitHubReleaseClient(http).DownloadAsync(snapshot.Asset(entry.Preview), MaxPreviewBytes,
                        entry.PreviewSha256, null, cancellation).ConfigureAwait(false);
                    Directory.CreateDirectory(folder);
                    await File.WriteAllBytesAsync(path, bytes, cancellation).ConfigureAwait(false);
                    File.SetLastWriteTimeUtc(path, Now.UtcDateTime);
                }
                if (PackageImages.Dimensions(path) != (PackagePreviews.Size, PackagePreviews.Size)) throw new PackageException(L.Text("A prévia do catálogo precisa ter 128 × 128 pixels."));
                return PackageImages.Load(path);
            }, cancellation).ConfigureAwait(false);
        }
        catch (Exception e) when (CatalogFailure.IsExpected(e)) { return null; }
        finally { _previews.Release(); }
    }
}
