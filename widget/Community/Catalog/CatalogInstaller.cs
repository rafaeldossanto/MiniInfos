using System.IO;
using System.Net.Http;
using MiniInfos.Infrastructure.Network;

namespace MiniInfos.Community.Catalog;

public sealed class CatalogInstaller(HttpClient http, PackageLibrary library)
{
    public async Task<InstalledPackage> InstallAsync(CatalogSnapshot snapshot, CatalogEntry entry, CancellationToken cancellation)
    {
        snapshot.Index.Validate();
        if (!snapshot.Index.Packages.Contains(entry)) throw new PackageException("O pacote não pertence à lista carregada.");
        var bytes = await new GitHubReleaseClient(http).DownloadAsync(snapshot.Asset(entry.Asset), entry.Size,
            entry.Sha256, entry.Size, cancellation).ConfigureAwait(false);
        var temporary = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellation).ConfigureAwait(false);
            return await Task.Run(() => library.Install(temporary, entry.Kind, entry.Id, entry.Version), cancellation).ConfigureAwait(false);
        }
        finally { File.Delete(temporary); }
    }
}
