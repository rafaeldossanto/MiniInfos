using System.IO;
using System.Text.Json;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Providers;

namespace MiniInfos.Providers.Claude;

public sealed record ContextFill(long? Tokens, long Window, double? Percent);

/// <summary>What the miniinfos mod of one chat last wrote to ~/.claude/miniinfos/sessions.</summary>
/// <param name="AliveAt">Rewritten every 30 s while the chat runs, wherever it runs.</param>
/// <param name="EndedAt">Stamped as the chat closes.</param>
public sealed record FeedEntry(
    string SessionId,
    string? Cwd,
    long UpdatedAt,
    long? AnsweredAt,
    long? AliveAt,
    long? EndedAt,
    IReadOnlyList<RateLimit>? RateLimits,
    ContextFill? Context,
    double? CostUsd)
{
    public DateTimeOffset Updated => DateTimeOffset.FromUnixTimeMilliseconds(UpdatedAt);

    public DateTimeOffset LastSeen => DateTimeOffset.FromUnixTimeMilliseconds(Math.Max(UpdatedAt, AliveAt ?? 0));
}

/// <param name="Latest">The chat that answered last.</param>
/// <param name="Limits">The plan's limits by kind, which are the account's, not a chat's.</param>
/// <param name="LimitsUpdated">When the chat holding the newest of those readings last wrote.</param>
/// <param name="HasOpenChat">Some Claude Code chat runs now: in the app, a terminal or an editor.</param>
public sealed record FeedState(
    FeedEntry? Latest,
    IReadOnlyDictionary<string, RateLimit> Limits,
    DateTimeOffset? LimitsUpdated,
    bool HasOpenChat);

public static class FeedReader
{
    private static readonly TimeSpan KeepFor = TimeSpan.FromDays(7);

    /// <summary>Three missed heartbeats: a chat that stopped without saying so (killed, crashed).</summary>
    private static readonly TimeSpan SilentFor = TimeSpan.FromSeconds(90);
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static FeedState Read(DateTimeOffset now) => Read(now, ClaudePaths.FeedFolders);

    public static FeedState Read(DateTimeOffset now, IReadOnlyList<string> folders)
    {
        var entries = new List<FeedEntry>();
        foreach (var folder in folders)
        {
            foreach (var file in SourceFiles.Files(folder, "*.json"))
            {
                if (TryRead(file) is not { } entry) continue;

                if (now - entry.LastSeen > KeepFor) TryDelete(file);
                else entries.Add(entry);
            }
        }

        var latest = entries.MaxBy(entry => (entry.AnsweredAt ?? 0, entry.UpdatedAt));
        var readings = entries
            .SelectMany(entry => (entry.RateLimits ?? []).Select(limit => (Entry: entry, Limit: limit)))
            .ToList();
        var limits = readings
            .GroupBy(reading => reading.Limit.Kind)
            .ToDictionary(group => group.Key, group => CurrentReading(group).Limit);
        DateTimeOffset? limitsUpdated = readings.Count > 0 ? readings.Max(reading => reading.Entry.Updated) : null;
        var hasOpenChat = entries.Any(entry => entry.EndedAt is null && entry.AliveAt is not null && now - entry.LastSeen <= SilentFor);

        return new FeedState(latest, limits, limitsUpdated, hasOpenChat);
    }

    /// <summary>
    /// Each chat keeps the reading of its own last answer, so an idle chat holds an old one.
    /// The current window is the one that resets last, and inside a window the use only grows:
    /// its highest reading is the newest.
    /// </summary>
    private static (FeedEntry Entry, RateLimit Limit) CurrentReading(IEnumerable<(FeedEntry Entry, RateLimit Limit)> readings) =>
        readings
            .OrderByDescending(reading => reading.Limit.ResetsAtTime ?? DateTimeOffset.MinValue)
            .ThenByDescending(reading => reading.Limit.PercentUsed)
            .First();

    private static FeedEntry? TryRead(string file)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonSerializer.Deserialize<FeedEntry>(stream, Options);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // Half written right now: the next read sees it whole.
            return null;
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
