using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using MiniInfos.Community;
using MiniInfos.Community.Catalog;
using MiniInfos.Infrastructure.Network;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Community;

public sealed class CatalogRuntimeTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task TheGitHubSourceUsesANumericRepositoryIdAndAVerifiedIndexThenReusesTheCache()
    {
        var (snapshot, body) = Snapshot();
        var index = snapshot.Index.Encode();
        var indexAsset = Asset("index.json", index);
        var release = new GitHubRelease("catalog", false, true, [indexAsset, .. snapshot.Assets]);
        var calls = 0;
        using var http = new HttpClient(new Answer(request =>
        {
            calls++;
            if (request.RequestUri!.Host == "api.github.com")
            {
                Assert.Equal($"/repositories/{GitHubRepository.Id}/releases/tags/catalog", request.RequestUri.AbsolutePath);
                return Reply(JsonSerializer.SerializeToUtf8Bytes(release));
            }
            return Reply(index);
        }));
        var cache = new CatalogCache(Path.Combine(_folder.Path, "cache"), () => Now);
        var source = new GitHubCatalog(http, GitHubRepository.Id, cache);
        Assert.Single((await source.LoadAsync(false, default)).Index.Packages);
        Assert.Single((await source.LoadAsync(false, default)).Index.Packages);
        Assert.Equal(2, calls);
        await source.LoadAsync(true, default);
        Assert.Equal(4, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InstallationRejectsWrongHashesAndBodiesLargerThanTheIndex(bool oversized)
    {
        var (snapshot, bytes) = Snapshot();
        using var http = new HttpClient(new Answer(_ =>
        {
            var response = Reply(oversized ? bytes.Concat(new byte[10]).ToArray() : new byte[bytes.Length]);
            response.Content.Headers.ContentLength = bytes.Length;
            return response;
        }));
        var library = new PackageLibrary(Path.Combine(_folder.Path, "library"));
        await Assert.ThrowsAsync<InvalidDataException>(() => new CatalogInstaller(http, library).InstallAsync(snapshot, snapshot.Index.Packages[0], default));
        Assert.Empty(library.List(PackageKind.Theme));
    }

    [Fact]
    public async Task AKnownHashDoesNotPermitInstallingAPackageOfTheWrongKind()
    {
        var (snapshot, bytes) = Snapshot();
        var entry = snapshot.Index.Packages[0] with { Kind = PackageKind.Mascot };
        snapshot = snapshot with { Index = snapshot.Index with { Packages = [entry] } };
        using var http = new HttpClient(new Answer(_ => Reply(bytes)));
        var library = new PackageLibrary(Path.Combine(_folder.Path, "library"));
        await Assert.ThrowsAsync<PackageException>(() => new CatalogInstaller(http, library).InstallAsync(snapshot, entry, default));
        Assert.Empty(library.List(PackageKind.Mascot));
        Assert.Empty(library.List(PackageKind.Theme));
    }

    [Fact]
    public async Task TheButtonsUseNumericVersionsAndASuccessfulDownloadInstallsAtomically()
    {
        var (snapshot, bytes) = Snapshot("2.10.0");
        using var http = new HttpClient(new Answer(_ => Reply(bytes)));
        var library = new PackageLibrary(Path.Combine(_folder.Path, "library"));
        var browser = new CatalogBrowser(new FixedSource(snapshot), http, new(Path.Combine(_folder.Path, "cache")), library);
        var entry = snapshot.Index.Packages[0];
        Assert.Equal(CatalogInstallState.Install, browser.State(entry));
        var archive = _folder.File("older.miniinfos");
        PackageLibrary.Create(archive, PackageFixture.Theme(version: "2.9.0"), new Dictionary<string, byte[]>());
        library.Install(archive, PackageKind.Theme);
        Assert.Equal(CatalogInstallState.Update, browser.State(entry));
        var installed = await browser.InstallAsync(snapshot, entry, default);
        Assert.Equal("2.10.0", installed.Manifest.Version);
        Assert.Equal(CatalogInstallState.Installed, browser.State(entry));
    }

    [Fact]
    public void TheIndexCacheExpiresAtOneHourAndRejectsFutureDates()
    {
        var (snapshot, _) = Snapshot();
        var current = Now;
        var cache = new CatalogCache(Path.Combine(_folder.Path, "cache"), () => current);
        cache.Save(snapshot);
        current += TimeSpan.FromMinutes(59);
        Assert.NotNull(cache.Load());
        current = Now + CatalogCache.ValidFor;
        Assert.Null(cache.Load());
        current = Now - TimeSpan.FromSeconds(1);
        Assert.Null(cache.Load());
    }

    [Fact]
    public async Task PreviewDownloadsAreFrozenCachedAndRequestedAgainOnlyAfterExpiry()
    {
        var (snapshot, _) = Snapshot();
        var png = PackageImages.Encode(PackagePreviews.Theme(new(PackageFixture.Theme(), _folder.Path)));
        var entry = snapshot.Index.Packages[0] with { PreviewSha256 = Hash(png) };
        snapshot = snapshot with { Index = snapshot.Index with { Packages = [entry] }, Assets = [snapshot.Assets[0], Asset(entry.Preview, png)] };
        var calls = 0;
        using var http = new HttpClient(new Answer(_ => { calls++; return Reply(png); }));
        var current = Now;
        var cache = new CatalogCache(Path.Combine(_folder.Path, "previews"), () => current);
        Assert.True(await cache.PreviewAsync(snapshot, entry, http, default) is { IsFrozen: true, PixelWidth: 128 });
        Assert.NotNull(await cache.PreviewAsync(snapshot, entry, http, default));
        Assert.Equal(1, calls);
        current += CatalogCache.ValidFor;
        Assert.NotNull(await cache.PreviewAsync(snapshot, entry, http, default));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ConcurrentCardsSharingArtworkDownloadItOnlyOnce()
    {
        var (snapshot, _) = Snapshot();
        var png = PackageImages.Encode(PackagePreviews.Theme(new(PackageFixture.Theme(), _folder.Path)));
        var entry = snapshot.Index.Packages[0] with { PreviewSha256 = Hash(png) };
        snapshot = snapshot with { Index = snapshot.Index with { Packages = [entry] }, Assets = [Asset(entry.Preview, png)] };
        var calls = 0;
        using var http = new HttpClient(new Answer(_ => { Interlocked.Increment(ref calls); return Reply(png); }));
        var cache = new CatalogCache(Path.Combine(_folder.Path, "previews"));
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => cache.PreviewAsync(snapshot, entry, http, default)));
        Assert.All(results, result => Assert.True(result is { IsFrozen: true }));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("https://example.com/catalog.miniinfos")]
    [InlineData("http://github.com/catalog.miniinfos")]
    [InlineData("https://github.com:8443/catalog.miniinfos")]
    public async Task CatalogDownloadsRejectUntrustedInitialUrlsBeforeMakingAnyRequest(string url)
    {
        var (snapshot, bytes) = Snapshot();
        var calls = 0;
        snapshot = snapshot with { Assets = [snapshot.Assets[0] with { Url = url }] };
        using var http = new HttpClient(new Answer(_ => { calls++; return Reply(bytes); }));
        await Assert.ThrowsAsync<InvalidDataException>(() => new CatalogInstaller(http, new(Path.Combine(_folder.Path, "library")))
            .InstallAsync(snapshot, snapshot.Index.Packages[0], default));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task RateLimitsKeepTheirHttpStatusAndABadIndexDigestIsRejected()
    {
        var cache = new CatalogCache(Path.Combine(_folder.Path, "cache"));
        using var limited = new HttpClient(new Answer(_ => new(HttpStatusCode.TooManyRequests)));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => new GitHubCatalog(limited, 42, cache).LoadAsync(false, default));
        Assert.Equal(HttpStatusCode.TooManyRequests, error.StatusCode);
        var (snapshot, _) = Snapshot();
        var asset = Asset("index.json", snapshot.Index.Encode()) with { Digest = "sha256:" + new string('0', 64) };
        using var bad = new HttpClient(new Answer(request => Reply(request.RequestUri!.Host == "api.github.com"
            ? JsonSerializer.SerializeToUtf8Bytes(new GitHubRelease("catalog", false, true, [asset, .. snapshot.Assets]))
            : snapshot.Index.Encode())));
        await Assert.ThrowsAsync<InvalidDataException>(() => new GitHubCatalog(bad, 42, cache).LoadAsync(false, default));
    }

    private (CatalogSnapshot Snapshot, byte[] Bytes) Snapshot(string version = "1.0.0")
    {
        var path = _folder.File("source.miniinfos");
        var manifest = PackageFixture.Theme(version: version);
        PackageLibrary.Create(path, manifest, new Dictionary<string, byte[]>());
        var bytes = File.ReadAllBytes(path);
        var asset = $"{manifest.Id}-{version}.miniinfos";
        var entry = new CatalogEntry(manifest.Id, manifest.Kind, manifest.Name, manifest.Author, version, manifest.License,
            "", asset, bytes.Length, Hash(bytes), $"{manifest.Id}-{version}.png", Hash("preview"u8.ToArray()));
        return (new(new(1, Now, [entry]), [Asset(asset, bytes), Asset(entry.Preview, "preview"u8.ToArray())]), bytes);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static GitHubAsset Asset(string name, byte[] bytes) => new(name,
        "https://github.com/test/catalog/releases/download/catalog-1/" + name, "sha256:" + Hash(bytes), bytes.Length);
    private static HttpResponseMessage Reply(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
    private sealed class FixedSource(CatalogSnapshot snapshot) : ICatalogSource
    {
        public Task<CatalogSnapshot> LoadAsync(bool force, CancellationToken cancellation) => Task.FromResult(snapshot);
    }
    private sealed class Answer(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(answer(request));
    }
}
