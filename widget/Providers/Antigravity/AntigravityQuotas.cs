using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using MiniInfos.Infrastructure.Desktop;

namespace MiniInfos.Providers.Antigravity;

internal sealed record AntigravityQuota(string Group, TimeSpan Length, RateLimit Limit);
internal sealed record AntigravityQuotaSnapshot(DateTimeOffset At, IReadOnlyList<AntigravityQuota> Buckets);

/// <summary>Reads the running agent's own quota summary. Never opens OAuth files or calls Google's endpoints directly.</summary>
internal sealed class AntigravityQuotas
{
    private readonly Func<CancellationToken, Task<string?>> _read;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AntigravityQuotaSnapshot? _snapshot;
    private DateTimeOffset _nextRead;

    public AntigravityQuotas(Func<CancellationToken, Task<string?>>? read = null) => _read = read ?? ReadLocal;

    public async Task<AntigravityQuotaSnapshot?> ReadAsync(DateTimeOffset now, bool visible, bool force)
    {
        await _gate.WaitAsync();
        try
        {
            if ((visible || force) && (force || now >= _nextRead))
            {
                _nextRead = now.AddMinutes(1);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                try
                {
                    var json = await _read(timeout.Token);
                    var buckets = json is not null ? Parse(json) : [];
                    if (buckets.Count > 0) _snapshot = new AntigravityQuotaSnapshot(now, buckets);
                }
                catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or System.IO.IOException)
                { /* Temporary failure keeps a recent reading, but never invents a percentage. */ }
            }
            return _snapshot is not null && now - _snapshot.At < TimeSpan.FromMinutes(5) ? _snapshot : null;
        }
        finally { _gate.Release(); }
    }

    internal static IReadOnlyList<AntigravityQuota> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("response", out var response)
            || response.ValueKind != JsonValueKind.Object || !response.TryGetProperty("groups", out var groups) || groups.ValueKind != JsonValueKind.Array) return [];
        var quotas = new List<AntigravityQuota>();
        foreach (var group in groups.EnumerateArray())
        {
            if (group.ValueKind != JsonValueKind.Object || !group.TryGetProperty("displayName", out var name) || name.ValueKind != JsonValueKind.String || name.GetString() is not { Length: > 0 and <= 80 } label || label.Any(char.IsControl)
                || !group.TryGetProperty("buckets", out var buckets) || buckets.ValueKind != JsonValueKind.Array) continue;
            foreach (var bucket in buckets.EnumerateArray())
            {
                if (bucket.ValueKind != JsonValueKind.Object || !bucket.TryGetProperty("window", out var window) || window.ValueKind != JsonValueKind.String) continue;
                var length = window.GetString() switch { "5h" => TimeSpan.FromHours(5), "daily" or "24h" => TimeSpan.FromDays(1), "weekly" => TimeSpan.FromDays(7), _ => TimeSpan.Zero };
                // Proto JSON omits scalar zeroes: an absent remainingFraction means exhausted, not unavailable.
                var left = 0d;
                if (bucket.TryGetProperty("remainingFraction", out var fraction) && (fraction.ValueKind != JsonValueKind.Number || !fraction.TryGetDouble(out left))) continue;
                if (length == TimeSpan.Zero || !double.IsFinite(left) || left < 0 || left > 1 || !bucket.TryGetProperty("resetTime", out var reset)
                    || reset.ValueKind != JsonValueKind.String || !DateTimeOffset.TryParse(reset.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _)) continue;
                quotas.Add(new AntigravityQuota(label, length, new RateLimit(window.GetString()!, (1 - left) * 100, reset.GetString())));
            }
        }
        return quotas;
    }

    private static async Task<string?> ReadLocal(CancellationToken cancel)
    {
        var endpoints = await AntigravityServerDiscovery.FindAsync(cancel);
        using var handler = new HttpClientHandler
        {
            UseProxy = false, AllowAutoRedirect = false,
            // The agent serves a self-signed certificate. This client reaches only PID-owned loopback listeners.
            ServerCertificateCustomValidationCallback = (request, _, _, _) => request.RequestUri is { IsLoopback: true, Scheme: "https" },
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(4) };
        foreach (var endpoint in endpoints)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://127.0.0.1:{endpoint.Port}/exa.language_server_pb.LanguageServerService/RetrieveUserQuotaSummary")
            {
                Version = new Version(2, 0), VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
                Content = new StringContent("{\"request\":{},\"forceRefresh\":false}", Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-Codeium-Csrf-Token", endpoint.Csrf);
            try
            {
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 262144) continue;
                await using var stream = await response.Content.ReadAsStreamAsync(cancel);
                using var output = new System.IO.MemoryStream();
                var buffer = new byte[8192];
                int count;
                while ((count = await stream.ReadAsync(buffer, cancel)) > 0)
                {
                    if (output.Length + count > 262144) return null;
                    output.Write(buffer, 0, count);
                }
                return Encoding.UTF8.GetString(output.ToArray());
            }
            catch (Exception e) when (e is HttpRequestException || e is OperationCanceledException && !cancel.IsCancellationRequested) { }
        }
        return null;
    }
}
