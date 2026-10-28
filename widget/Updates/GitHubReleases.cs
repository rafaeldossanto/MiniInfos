using System.Net.Http;
using System.IO;
using System.Text.Json;
using MiniInfos.Infrastructure.Network;

namespace MiniInfos.Updates;

/// <summary>
/// The releases of MiniInfos on GitHub. The repository is asked for by its number, which GitHub answers
/// as it does by name, so no account name ships in the app.
/// </summary>
public sealed class GitHubReleases(HttpClient http, string assetName = GitHubReleases.InstallerName) : IUpdateSource
{
    /// <summary>The asset each release carries, built by installer\build.ps1.</summary>
    public const string InstallerName = "MiniInfos-Setup.exe";

    private const string DigestPrefix = "sha256:";

    /// <summary>One client for the app: GitHub asks every caller to say who it is.</summary>
    public static HttpClient CreateClient() => GitHubReleaseClient.CreateClient();

    public async Task<AvailableUpdate?> NewerThanAsync(Version current, CancellationToken cancellation)
    {
        try
        {
            return FromRelease(await new GitHubReleaseClient(http).LatestAsync(GitHubRepository.Id, cancellation), current, assetName);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or IOException or InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>
    /// A release as GitHub describes it, kept only when it is newer, published, and carries the installer
    /// on github.com with the digest it must match.
    /// </summary>
    public static AvailableUpdate? Parse(string json, Version current, string assetName = InstallerName) => FromRelease(GitHubReleaseClient.Parse(json), current, assetName);

    private static AvailableUpdate? FromRelease(GitHubRelease? release, Version current, string assetName)
    {
        if (release is null || release.Draft || release.Prerelease) return null;
        if (!Version.TryParse(release.Tag?.TrimStart('v', 'V'), out var version) || Comparable(version) <= Comparable(current)) return null;

        var installer = release.Assets?.FirstOrDefault(asset => asset.Name == assetName);
        if (installer?.Digest is not { } digest || !digest.StartsWith(DigestPrefix, StringComparison.OrdinalIgnoreCase)) return null;
        if (digest.Length != 71 || !digest.AsSpan(7).ToString().All(Uri.IsHexDigit)) return null;
        if (!Uri.TryCreate(installer.Url, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps || url.Host != "github.com") return null;

        return new AvailableUpdate(version, url, digest[DigestPrefix.Length..].ToUpperInvariant());
    }

    /// <summary>"1.4" and "1.4.0" are the same version.</summary>
    private static Version Comparable(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build));

}
