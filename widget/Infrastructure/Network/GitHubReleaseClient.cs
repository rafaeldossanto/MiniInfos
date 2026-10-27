using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MiniInfos.Infrastructure.Network;

public sealed record GitHubRelease(
    [property: JsonPropertyName("tag_name")] string? Tag,
    [property: JsonPropertyName("draft")] bool Draft,
    [property: JsonPropertyName("prerelease")] bool Prerelease,
    [property: JsonPropertyName("assets")] GitHubAsset[]? Assets);

public sealed record GitHubAsset(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("browser_download_url")] string? Url,
    [property: JsonPropertyName("digest")] string? Digest,
    [property: JsonPropertyName("size")] long? Size);

/// <summary>GitHub release metadata and bounded, digest-checked asset downloads, shared by updates and the catalog.</summary>
public sealed class GitHubReleaseClient(HttpClient http)
{
    public static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"MiniInfos/{AppInfo.Version}");
        return client;
    }

    public static GitHubRelease? Parse(string json) => JsonSerializer.Deserialize<GitHubRelease>(json);

    public Task<GitHubRelease?> LatestAsync(long repositoryId, CancellationToken cancellation) =>
        ReleaseAsync(repositoryId, "latest", cancellation);

    public Task<GitHubRelease?> ByTagAsync(long repositoryId, string tag, CancellationToken cancellation) =>
        ReleaseAsync(repositoryId, "tags/" + Uri.EscapeDataString(tag), cancellation);

    private async Task<GitHubRelease?> ReleaseAsync(long repositoryId, string endpoint, CancellationToken cancellation)
    {
        if (repositoryId <= 0) throw new ArgumentOutOfRangeException(nameof(repositoryId));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repositories/{repositoryId}/releases/{endpoint}");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var bytes = await ReadBoundedAsync(response, 16 * 1024 * 1024, cancellation).ConfigureAwait(false);
        return JsonSerializer.Deserialize<GitHubRelease>(bytes);
    }

    public async Task<byte[]> DownloadAsync(GitHubAsset asset, long maximum, string? expectedSha256,
        long? exactLength, CancellationToken cancellation)
    {
        if (!Uri.TryCreate(asset.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com"
            || !uri.IsDefaultPort || uri.UserInfo.Length != 0)
            throw new InvalidDataException(L.Text("O arquivo do catálogo precisa vir do GitHub por HTTPS."));
        var digest = asset.Digest;
        if (digest is null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            || digest.Length != 71 || !digest.AsSpan(7).ToString().All(Uri.IsHexDigit))
            throw new InvalidDataException(L.Text("O GitHub não informou um SHA-256 válido para o arquivo."));
        var hash = digest[7..];
        if (expectedSha256 is not null && !hash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(L.Text("O SHA-256 do arquivo difere do anunciado no catálogo."));
        if (asset.Size < 0 || asset.Size > maximum || (asset.Size is { } announced && exactLength is { } exact && announced != exact))
            throw new InvalidDataException(L.Text("O tamanho do arquivo difere do anunciado no catálogo."));
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var bytes = await ReadBoundedAsync(response, maximum, cancellation).ConfigureAwait(false);
        if ((exactLength is { } length && bytes.LongLength != length)
            || !Convert.ToHexString(SHA256.HashData(bytes)).Equals(hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(L.Text("O arquivo recebido não confere com o tamanho ou o SHA-256 anunciado."));
        return bytes;
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage response, long maximum, CancellationToken cancellation)
    {
        if (response.Content.Headers.ContentLength > maximum) throw new InvalidDataException(L.Text("O arquivo excede o tamanho permitido."));
        using var input = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellation).ConfigureAwait(false)) != 0)
        {
            if (output.Length + read > maximum) throw new InvalidDataException(L.Text("O arquivo excede o tamanho permitido."));
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
}
