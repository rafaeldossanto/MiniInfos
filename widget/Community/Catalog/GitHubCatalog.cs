using System.IO;
using System.Net.Http;
using MiniInfos.Infrastructure.Network;

namespace MiniInfos.Community.Catalog;

public sealed class GitHubCatalog(HttpClient http, long repositoryId, CatalogCache cache) : ICatalogSource
{
    public async Task<CatalogSnapshot> LoadAsync(bool force, CancellationToken cancellation)
    {
        if (!force && await Task.Run(cache.Load, cancellation).ConfigureAwait(false) is { } cached) return cached;
        var client = new GitHubReleaseClient(http);
        var release = await client.ByTagAsync(repositoryId, GitHubRepository.CatalogTag, cancellation).ConfigureAwait(false);
        if (release is null || release.Tag != GitHubRepository.CatalogTag || release.Draft || !release.Prerelease || release.Assets is null)
            throw new PackageException("Nenhum catálogo publicado foi encontrado.");
        var indexes = release.Assets.Where(asset => asset.Name == "index.json").ToArray();
        if (indexes.Length != 1) throw new PackageException("A publicação precisa conter um único index.json.");
        var bytes = await client.DownloadAsync(indexes[0], CatalogIndex.MaxBytes, null, null, cancellation).ConfigureAwait(false);
        var snapshot = new CatalogSnapshot(CatalogIndex.Read(bytes), release.Assets);
        foreach (var entry in snapshot.Index.Packages)
        {
            snapshot.Asset(entry.Asset);
            snapshot.Asset(entry.Preview);
        }
        await Task.Run(() => cache.Save(snapshot), cancellation).ConfigureAwait(false);
        return snapshot;
    }
}
