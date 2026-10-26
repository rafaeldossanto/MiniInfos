using System.Text.Json;

namespace MiniInfos.Tests;

public sealed class CodexUsageLedgerTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Now;

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void CumulativeReadingsCountOnceAndCachedInputOnlyOnce()
    {
        WriteLog(
            Meta(),
            Reading(At, input: 100, cached: 20, output: 50, total: 150),
            Reading(At.AddSeconds(1), input: 100, cached: 20, output: 50, total: 150),
            Reading(At.AddSeconds(2), input: 200, cached: 40, output: 80, total: 280));

        var tokens = Scan().Tokens.Total;

        Assert.Equal(280, tokens.Total);
        Assert.Equal(160, tokens.Input);
        Assert.Equal(40, tokens.CacheRead);
        Assert.Equal(80, tokens.Output);
    }

    [Fact]
    public void AReadingWithOnlyTheTotalStillCountsAllItsTokens()
    {
        // A compaction's reading fills in total_tokens alone: it was 17% of the tokens left out.
        WriteLog(
            Reading(At, input: 0, cached: 0, output: 0, total: 98_363),
            Reading(At.AddSeconds(1), input: 1_000, cached: 200, output: 300, total: 99_663));

        Assert.Equal(99_663, Scan().Tokens.Total.Total);
    }

    [Fact]
    public void ANewSessionAndTheProjectAndContextOfTheLatestOne()
    {
        WriteLog(Meta(cwd: @"C:\code\MiniInfos"), Reading(At, input: 500, cached: 0, output: 10, total: 510, lastInput: 100, window: 200));

        var snapshot = Scan();

        Assert.Equal("MiniInfos", snapshot.LatestProject);
        Assert.Equal(50, snapshot.ContextPercent);
        Assert.NotNull(snapshot.UpdatedAt);
    }

    [Fact]
    public void ALineStillBeingWrittenWaitsForItsNewline()
    {
        var log = WriteLog(Reading(At, input: 100, cached: 0, output: 0, total: 100));
        File.AppendAllText(log, "{\"type\":\"event_msg\"");
        Assert.Equal(100, Scan().Tokens.Total.Total);

        File.AppendAllText(log, ",\"payload\":{}}\n" + Reading(At.AddSeconds(1), input: 150, cached: 0, output: 0, total: 150) + "\n");
        Assert.Equal(150, Scan().Tokens.Total.Total);
    }

    [Fact]
    public void ARestartResumesFromTheSavedCursorWithoutCountingAgain()
    {
        var log = WriteLog(Reading(At, input: 200, cached: 0, output: 80, total: 280));
        var ledger = Ledger();
        ledger.Scan();
        ledger.Flush();

        File.AppendAllText(log, Reading(At.AddSeconds(1), input: 250, cached: 0, output: 100, total: 350) + "\n");
        var reopened = Ledger();
        reopened.Scan();

        Assert.Equal(350, reopened.Summarize(At, At.AddHours(-5), At.AddDays(-7)).Tokens.Total.Total);
    }

    [Fact]
    public void CopiesInDifferentHomesCountOnceAndNewGrowthStillCounts()
    {
        var original = WriteLog(Meta(id: "same-session"), Reading(At, 100, 20, 50, 150));
        var copy = _folder.File("other-home", "copied.jsonl");
        File.Copy(original, copy);
        IReadOnlyList<string> sources = [Path.Combine(_folder.Path, "sessions"), Path.Combine(_folder.Path, "other-home")];
        var ledger = CodexUsageLedger.LoadSources(() => sources, Path.Combine(_folder.Path, "codex-ledger.json"));
        ledger.Scan();
        Assert.Equal(150, Total(ledger));

        File.AppendAllText(copy, Reading(At.AddSeconds(1), 200, 40, 80, 280) + "\n");
        ledger.Scan();
        Assert.Equal(280, Total(ledger));

        // Sources can disappear between reads; the session's maximum survives cursor cleanup and app restarts.
        sources = [];
        ledger.Scan();
        ledger.Flush();
        sources = [Path.Combine(_folder.Path, "other-home")];
        var restarted = CodexUsageLedger.LoadSources(() => sources, Path.Combine(_folder.Path, "codex-ledger.json"));
        restarted.Scan();
        Assert.Equal(280, Total(restarted));
        File.AppendAllText(copy, Reading(At.AddSeconds(2), 300, 60, 100, 400) + "\n");
        restarted.Scan();
        Assert.Equal(400, Total(restarted));
    }

    [Fact]
    public void MovingASessionToTheArchiveDoesNotLoseOrRepeatTokens()
    {
        var original = WriteLog(Meta(id: "archive-me"), Reading(At, 100, 0, 0, 100));
        IReadOnlyList<string> sources = [Path.Combine(_folder.Path, "sessions")];
        var ledger = CodexUsageLedger.LoadSources(() => sources, Path.Combine(_folder.Path, "codex-ledger.json"));
        ledger.Scan();
        var archive = _folder.File("archived_sessions", "archived.jsonl");
        File.Move(original, archive);
        File.AppendAllText(archive, Reading(At.AddSeconds(1), 150, 0, 0, 150) + "\n");
        sources = [sources[0], Path.Combine(_folder.Path, "archived_sessions")];
        ledger.Scan();

        Assert.Equal(150, Total(ledger));
    }

    [Fact]
    public void FullCopiesProvideTheHistoricalDatesBeforePartialCopies()
    {
        WriteLog(Meta(id: "partial-copy"), Reading(At.AddDays(-1), 100, 0, 0, 100), Reading(At, 200, 0, 0, 200));
        File.WriteAllText(_folder.File("other", "partial.jsonl"), Meta(id: "partial-copy") + "\n" + Reading(At, 200, 0, 0, 200) + "\n");
        var ledger = CodexUsageLedger.LoadSources(() => [Path.Combine(_folder.Path, "other"), Path.Combine(_folder.Path, "sessions")],
            Path.Combine(_folder.Path, "codex-ledger.json"));
        ledger.Scan();

        var tokens = ledger.Summarize(At, At.AddHours(-5), At.AddDays(-7)).Tokens;
        Assert.Equal(200, tokens.Total.Total);
        Assert.Equal(100, tokens.Today.Total);
    }

    [Fact]
    public void AnOldLedgerWithAMissingCursorRecognizesAMovedRolloutByItsUuid()
    {
        const string id = "019a1234-abcd-7000-8000-123456789012";
        var store = Path.Combine(_folder.Path, "codex-ledger.json");
        var oldPath = Path.Combine(_folder.Path, "gone", $"rollout-2026-10-25T12-00-00-{id}.jsonl");
        File.WriteAllText(store, JsonSerializer.Serialize(new
        {
            Cursors = new Dictionary<string, object>(),
            Readings = new Dictionary<string, LedgerEntry> { [$"{oldPath}:150"] = new(At.ToUnixTimeSeconds(), 80, 50, 20, 0) },
        }));
        var moved = _folder.File("sessions", $"rollout-{id}.jsonl");
        File.WriteAllText(moved, Meta(id: id) + "\n" + Reading(At, 100, 20, 50, 150) + "\n" + Reading(At.AddSeconds(1), 200, 40, 80, 280) + "\n");

        var ledger = Ledger();
        ledger.Scan();

        Assert.Equal(280, Total(ledger));
    }

    private static long Total(CodexUsageLedger ledger) => ledger.Summarize(At, At.AddHours(-5), At.AddDays(-7)).Tokens.Total.Total;

    [Fact]
    public void ALogRewrittenShorterIsReadAgainWithoutCountingTwice()
    {
        var log = WriteLog(Reading(At, input: 100, cached: 0, output: 0, total: 100), Reading(At.AddSeconds(1), input: 300, cached: 0, output: 0, total: 300));
        var ledger = Ledger();
        ledger.Scan();

        File.WriteAllText(log, Reading(At, input: 100, cached: 0, output: 0, total: 100) + "\n");
        ledger.Scan();

        Assert.Equal(300, ledger.Summarize(At, At.AddHours(-5), At.AddDays(-7)).Tokens.Total.Total);
    }

    private CodexUsageSnapshot Scan()
    {
        var ledger = Ledger();
        ledger.Scan();
        return ledger.Summarize(At, At.AddHours(-5), At.AddDays(-7));
    }

    private CodexUsageLedger Ledger() =>
        CodexUsageLedger.Load(Path.Combine(_folder.Path, "sessions"), Path.Combine(_folder.Path, "codex-ledger.json"));

    private string WriteLog(params string[] lines)
    {
        var log = _folder.File("sessions", "2026", "10", "25", "rollout.jsonl");
        File.WriteAllText(log, string.Join("\n", lines) + "\n");
        return log;
    }

    private static string Meta(string cwd = @"C:\code\project", string? id = null) =>
        JsonSerializer.Serialize(new { timestamp = At, type = "session_meta", payload = new { cwd, id } });

    private static string Reading(DateTimeOffset at, long input, long cached, long output, long total, long lastInput = 0, long window = 0) =>
        JsonSerializer.Serialize(new
        {
            timestamp = at,
            type = "event_msg",
            payload = new
            {
                type = "token_count",
                info = new
                {
                    total_token_usage = new
                    {
                        input_tokens = input,
                        cached_input_tokens = cached,
                        cache_write_input_tokens = 0,
                        output_tokens = output,
                        total_tokens = total,
                    },
                    last_token_usage = new { input_tokens = lastInput },
                    model_context_window = window,
                },
            },
        });
}
