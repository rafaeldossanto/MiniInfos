using MiniInfos.Infrastructure.Network;

namespace MiniInfos.Community.Catalog;

public sealed record CatalogSnapshot(CatalogIndex Index, GitHubAsset[] Assets)
{
    public GitHubAsset Asset(string name)
    {
        var matches = Assets.Where(asset => asset.Name == name).ToArray();
        return matches.Length == 1 ? matches[0] : throw new PackageException($"Arquivo ausente ou duplicado na publicação: {name}.");
    }
}

public interface ICatalogSource
{
    Task<CatalogSnapshot> LoadAsync(bool force, CancellationToken cancellation);
}

