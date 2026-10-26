using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using MiniInfos.Infrastructure;
using MiniInfos.Providers;

namespace MiniInfos.Providers.Codex;

public enum CodexAccountStatus
{
    /// <summary>No reading yet.</summary>
    Waiting,

    /// <summary>The last reading came back.</summary>
    Ready,

    /// <summary>No codex to ask.</summary>
    NotInstalled,

    /// <summary>codex is there but gave no limits (signed out, offline, too slow).</summary>
    Unavailable,
}

public sealed record CodexAccountState(
    PlanWindow? Primary,
    PlanWindow? Secondary,
    DateTimeOffset? UpdatedAt,
    CodexAccountStatus Status)
{
    public static CodexAccountState Waiting { get; } = new(null, null, null, CodexAccountStatus.Waiting);
}

/// <summary>
/// Reads the Codex account's limits through the local codex app-server, never from credentials.
/// Reads run apart from the card's refresh, at most one at a time: the card draws the last one.
/// </summary>
public sealed class CodexAccountReader
{
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromSeconds(15);

    private readonly Lock _gate = new();
    private Task? _pending;
    private DateTimeOffset _lastAttempt = DateTimeOffset.MinValue;
    private CodexAccountState _current = CodexAccountState.Waiting;

    public CodexAccountState Current
    {
        get
        {
            lock (_gate) return _current;
        }
    }

    /// <summary>Starts a read when the last one is a minute old and none runs; never waits for it.</summary>
    public void RefreshIfDue()
    {
        lock (_gate)
        {
            if (_pending is null && DateTimeOffset.Now - _lastAttempt >= RefreshEvery) Start();
        }
    }

    /// <summary>Reads now, or joins the read already running, and returns once it is done.</summary>
    public Task RefreshAsync()
    {
        lock (_gate)
        {
            return _pending ?? Start();
        }
    }

    /// <summary>Called under the gate.</summary>
    private Task Start()
    {
        _lastAttempt = DateTimeOffset.Now;
        return _pending = Task.Run(async () =>
        {
            var read = await FetchAsync();
            lock (_gate)
            {
                // A failed read keeps the last good numbers; with none, it says why there are none.
                if (read.Status == CodexAccountStatus.Ready || _current.Status != CodexAccountStatus.Ready) _current = read;
                _pending = null;
            }
        });
    }

    private static async Task<CodexAccountState> FetchAsync()
    {
        if (CodexCli.AppServer() is not { } start) return CodexAccountState.Waiting with { Status = CodexAccountStatus.NotInstalled };

        var unavailable = CodexAccountState.Waiting with { Status = CodexAccountStatus.Unavailable };
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardInput = true;
        start.RedirectStandardOutput = true;

        try
        {
            using var giveUp = new CancellationTokenSource(GiveUpAfter);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("codex did not start.");
            try
            {
                await SendAsync(process, 1, "initialize", new
                {
                    clientInfo = new { name = "miniinfos", title = "MiniInfos", version = AppInfo.Version },
                }, giveUp.Token);
                using var initialized = await ReadResponseAsync(process, 1, giveUp.Token);
                if (initialized.RootElement.TryGetProperty("error", out _)) return unavailable;

                await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"method\":\"initialized\",\"params\":{}}".AsMemory(), giveUp.Token);
                await SendAsync(process, 2, "account/rateLimits/read", new { }, giveUp.Token);
                using var limits = await ReadResponseAsync(process, 2, giveUp.Token);

                var (primary, secondary) = ParseLimits(limits.RootElement);
                return primary is null && secondary is null
                    ? unavailable
                    : new CodexAccountState(primary, secondary, DateTimeOffset.Now, CodexAccountStatus.Ready);
            }
            finally
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or JsonException or InvalidOperationException or OperationCanceledException)
        {
            return unavailable;
        }
    }

    private static async Task SendAsync(Process process, int id, string method, object parameters, CancellationToken cancellation)
    {
        var message = JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters });
        await process.StandardInput.WriteLineAsync(message.AsMemory(), cancellation);
        await process.StandardInput.FlushAsync(cancellation);
    }

    private static async Task<JsonDocument> ReadResponseAsync(Process process, int id, CancellationToken cancellation)
    {
        while (await process.StandardOutput.ReadLineAsync(cancellation) is { } line)
        {
            var document = JsonDocument.Parse(line);
            if (document.RootElement.TryGetProperty("id", out var received)
                && received.ValueKind == JsonValueKind.Number && received.GetInt32() == id)
                return document;
            document.Dispose();
        }

        throw new IOException("Codex app-server closed before replying.");
    }

    /// <summary>The account's two windows from an account/rateLimits/read reply: the codex bucket, or the older single one.</summary>
    internal static (PlanWindow? Primary, PlanWindow? Secondary) ParseLimits(JsonElement response)
    {
        if (!response.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object) return (null, null);

        JsonElement bucket;
        if (result.TryGetProperty("rateLimitsByLimitId", out var byId)
            && byId.ValueKind == JsonValueKind.Object && byId.TryGetProperty("codex", out var codex))
            bucket = codex;
        else if (result.TryGetProperty("rateLimits", out var single))
            bucket = single;
        else return (null, null);

        return (ParseWindow(bucket, "primary"), ParseWindow(bucket, "secondary"));
    }

    private static PlanWindow? ParseWindow(JsonElement bucket, string name)
    {
        if (bucket.ValueKind != JsonValueKind.Object || !bucket.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Object) return null;
        if (!value.TryGetProperty("usedPercent", out var percent) || percent.ValueKind != JsonValueKind.Number
            || !percent.TryGetDouble(out var used)) return null;

        var minutes = value.TryGetProperty("windowDurationMins", out var duration)
            && duration.ValueKind == JsonValueKind.Number && duration.TryGetInt32(out var length)
            ? length : 0;
        var reset = value.TryGetProperty("resetsAt", out var timestamp)
            && timestamp.ValueKind == JsonValueKind.Number && timestamp.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).ToString("O", CultureInfo.InvariantCulture)
            : null;
        var window = minutes > 0 ? TimeSpan.FromMinutes(minutes) : DefaultLength(name);

        return new PlanWindow(window, new RateLimit(name, used, reset));
    }

    /// <summary>A reading that leaves its length out: the plan's usual 5 hours, or week.</summary>
    private static TimeSpan DefaultLength(string name) => name == "primary" ? TimeSpan.FromHours(5) : TimeSpan.FromDays(7);
}
