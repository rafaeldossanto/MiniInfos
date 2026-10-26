using System.Net.Http;
using System.Windows.Media.Imaging;
using MiniInfos.Infrastructure.Network;
using MiniInfos.Infrastructure.Storage;

namespace MiniInfos.Community.Catalog;

public enum CatalogInstallState { Install, Installed, Update }

/// <summary>Catalog operations shared by the settings page and its offline, injected-source checks.</summary>
public sealed class CatalogBrowser(ICatalogSource source, HttpClient http, CatalogCache cache, PackageLibrary library)
{
    private static readonly HttpClient SharedHttp = GitHubReleaseClient.CreateClient();
    public PackageLibrary Library => library;

    public static CatalogBrowser CreateDefault()
    {
        var cache = new CatalogCache(System.IO.Path.Combine(AppPaths.Data, "catalog"));
        ICatalogSource source = new GitHubCatalog(SharedHttp, GitHubRepository.Id, cache);
        return new(source, SharedHttp, cache, PackageLibrary.Shared);
    }

    public Task<CatalogSnapshot> LoadAsync(bool force, CancellationToken cancellation) => source.LoadAsync(force, cancellation);
    public Task<BitmapSource?> PreviewAsync(CatalogSnapshot snapshot, CatalogEntry entry, CancellationToken cancellation) =>
        cache.PreviewAsync(snapshot, entry, http, cancellation);
    public Task<InstalledPackage> InstallAsync(CatalogSnapshot snapshot, CatalogEntry entry, CancellationToken cancellation) =>
        new CatalogInstaller(http, library).InstallAsync(snapshot, entry, cancellation);

    public CatalogInstallState State(CatalogEntry entry)
    {
        var installed = library.Find(entry.Kind, entry.Id);
        if (installed is null) return CatalogInstallState.Install;
        return PackageValidation.ParseVersion(entry.Version) > PackageValidation.ParseVersion(installed.Manifest.Version)
            ? CatalogInstallState.Update : CatalogInstallState.Installed;
    }

}
