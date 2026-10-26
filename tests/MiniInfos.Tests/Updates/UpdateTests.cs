using System.Net.Http;
using System.Net;
using System.Security.Cryptography;
using MiniInfos.Updates;

namespace MiniInfos.Tests.Updates;

/// <summary>Finding a newer release on GitHub, and installing only the bytes it announced.</summary>
public sealed class UpdateTests
{
    private static readonly Version Current = new(1, 3, 0);
    private static readonly byte[] InstallerBytes = "MZ fake installer"u8.ToArray();
    private static readonly string InstallerHash = Convert.ToHexString(SHA256.HashData(InstallerBytes));

    [Fact]
    public void ANewerReleaseWithItsInstallerIsAnUpdate()
    {
        var update = GitHubReleases.Parse(Release("v1.4.0"), Current);

        Assert.NotNull(update);
        Assert.Equal(new Version(1, 4, 0), update.Version);
        Assert.Equal("github.com", update.Installer.Host);
        Assert.Equal(InstallerHash, update.Sha256);
    }

    [Theory]
    [InlineData("v1.3.0")]
    [InlineData("1.3")]
    [InlineData("v1.2.9")]
    [InlineData("nightly")]
    public void TheSameOrAnOlderVersionIsNot(string tag) => Assert.Null(GitHubReleases.Parse(Release(tag), Current));

    [Fact]
    public void AReleaseIsSkippedWhenItsInstallerCantBeTrusted()
    {
        Assert.Null(GitHubReleases.Parse(Release("v1.4.0", digest: null), Current));
        Assert.Null(GitHubReleases.Parse(Release("v1.4.0", host: "example.com"), Current));
        Assert.Null(GitHubReleases.Parse(Release("v1.4.0", asset: "Other.exe"), Current));
        Assert.Null(GitHubReleases.Parse(Release("v1.4.0", draft: true), Current));
    }

    [Fact]
    public async Task ACheckFindingAnUpdateTellsItOnce()
    {
        var update = GitHubReleases.Parse(Release("v1.4.0"), Current)!;
        using var updater = new Updater(new Source(update), Client(InstallerBytes), Current, _ => { });
        var told = 0;
        updater.Changed += () => told++;

        await updater.CheckAsync();
        await updater.CheckAsync();

        Assert.Equal((UpdateStage.Available, 1), (updater.Stage, told));
        Assert.Equal(update, updater.Available);
    }

    [Fact]
    public async Task TheDownloadedInstallerRunsOnlyWhenItMatchesTheRelease()
    {
        var update = GitHubReleases.Parse(Release("v1.4.0"), Current)!;
        string? launched = null;
        using var updater = new Updater(new Source(update), Client(InstallerBytes), Current, path => launched = path);
        await updater.CheckAsync();

        await updater.InstallAsync();

        Assert.Equal(UpdateStage.Installing, updater.Stage);
        Assert.NotNull(launched);
        Assert.Equal(InstallerBytes, File.ReadAllBytes(launched));
        File.Delete(launched);
    }

    [Fact]
    public async Task AnInstallerThatDoesntMatchIsDeletedAndNeverRuns()
    {
        var update = GitHubReleases.Parse(Release("v1.4.0"), Current)!;
        var launched = false;
        using var updater = new Updater(new Source(update), Client("MZ something else"u8.ToArray()), Current, _ => launched = true);
        await updater.CheckAsync();

        await updater.InstallAsync();

        Assert.Equal(UpdateStage.Failed, updater.Stage);
        Assert.False(launched);
        Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), "MiniInfos-update", "MiniInfos-Setup-1.4.0.exe")));
    }

    private static string Release(string tag, string? digest = "", string host = "github.com", string asset = GitHubReleases.InstallerName, bool draft = false)
    {
        var digestJson = digest is null ? "null" : $"\"sha256:{(digest == "" ? InstallerHash.ToLowerInvariant() : digest)}\"";
        return $$"""
            {
              "tag_name": "{{tag}}",
              "draft": {{(draft ? "true" : "false")}},
              "prerelease": false,
              "assets": [
                {
                  "name": "{{asset}}",
                  "browser_download_url": "https://{{host}}/owner/MiniInfos/releases/download/{{tag}}/{{asset}}",
                  "digest": {{digestJson}}
                }
              ]
            }
            """;
    }

    private static HttpClient Client(byte[] body) => new(new Answer(body));

    private sealed class Source(AvailableUpdate update) : IUpdateSource
    {
        public Task<AvailableUpdate?> NewerThanAsync(Version current, CancellationToken cancellation) => Task.FromResult<AvailableUpdate?>(update);
    }

    /// <summary>Answers every request with the same bytes, as GitHub would serve the installer.</summary>
    private sealed class Answer(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
    }
}
