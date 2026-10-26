using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MiniInfos;

/// <summary>A newer MiniInfos, ready to download: its installer and the SHA-256 the file must have.</summary>
public sealed record AvailableUpdate(Version Version, Uri Installer, string Sha256);

/// <summary>Where the latest version is announced.</summary>
public interface IUpdateSource
{
    /// <summary>The latest release when it is newer than <paramref name="current"/>; null otherwise, and when it can't be read.</summary>
    Task<AvailableUpdate?> NewerThanAsync(Version current, CancellationToken cancellation);
}

/// <summary>
/// The releases of MiniInfos on GitHub. The repository is asked for by its number, which GitHub answers
/// as it does by name, so no account name ships in the app.
/// </summary>
public sealed class GitHubReleases(HttpClient http) : IUpdateSource
{
    /// <summary>The asset each release carries, built by installer\build.ps1.</summary>
    public const string InstallerName = "MiniInfos-Setup.exe";

    private const long RepositoryId = 1432597159;
    private const string DigestPrefix = "sha256:";
    private static readonly Uri Latest = new($"https://api.github.com/repositories/{RepositoryId}/releases/latest");

    /// <summary>One client for the app: GitHub asks every caller to say who it is.</summary>
    public static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"MiniInfos/{AppInfo.Version}");
        return client;
    }

    public async Task<AvailableUpdate?> NewerThanAsync(Version current, CancellationToken cancellation)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Latest);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await http.SendAsync(request, cancellation);
            if (!response.IsSuccessStatusCode) return null;
            return Parse(await response.Content.ReadAsStringAsync(cancellation), current);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// A release as GitHub describes it, kept only when it is newer, published, and carries the installer
    /// on github.com with the digest it must match.
    /// </summary>
    public static AvailableUpdate? Parse(string json, Version current)
    {
        var release = JsonSerializer.Deserialize<Release>(json);
        if (release is null || release.Draft || release.Prerelease) return null;
        if (!Version.TryParse(release.Tag?.TrimStart('v', 'V'), out var version) || Comparable(version) <= Comparable(current)) return null;

        var installer = release.Assets?.FirstOrDefault(asset => asset.Name == InstallerName);
        if (installer?.Digest is not { } digest || !digest.StartsWith(DigestPrefix, StringComparison.OrdinalIgnoreCase)) return null;
        if (!Uri.TryCreate(installer.Url, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps || url.Host != "github.com") return null;

        return new AvailableUpdate(version, url, digest[DigestPrefix.Length..].ToUpperInvariant());
    }

    /// <summary>"1.4" and "1.4.0" are the same version.</summary>
    private static Version Comparable(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build));

    private sealed record Release(
        [property: JsonPropertyName("tag_name")] string? Tag,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool Prerelease,
        [property: JsonPropertyName("assets")] IReadOnlyList<Asset>? Assets);

    private sealed record Asset(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("browser_download_url")] string? Url,
        [property: JsonPropertyName("digest")] string? Digest);
}
