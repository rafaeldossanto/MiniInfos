using System.Text.Json;

namespace MiniInfos.Tests;

public sealed class ClaudeUsageLedgerTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Now;

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void AResponseWrittenOncePerBlockCountsOnceAtItsFullest()
    {
        WriteTranscript(
            Response("msg_1", input: 10, output: 5),
            Response("msg_1", input: 10, output: 40),
            Response("msg_2", input: 3, output: 2, cacheRead: 1_000, cacheWrite: 50));

        var tokens = Summarize(Ledger()).Total;

        Assert.Equal(10 + 40 + 3 + 2 + 1_000 + 50, tokens.Total);
        Assert.Equal(1_000, tokens.CacheRead);
    }

    [Fact]
    public void LinesThatAreNotResponsesAreLeftOut()
    {
        WriteTranscript(
            JsonSerializer.Serialize(new { type = "user", timestamp = At, message = new { id = "u1", usage = new { input_tokens = 99 } } }),
            "not json \"usage\"",
            Response("msg_1", input: 7, output: 3));

        Assert.Equal(10, Summarize(Ledger()).Total.Total);
    }

    [Fact]
    public void ALedgerSavedBy100IsReadAsItWas()
    {
        // The file of 1.0.0, so an update keeps the history of transcripts already cleaned up.
        var store = Path.Combine(_folder.Path, "ledger.json");
        File.WriteAllText(store, JsonSerializer.Serialize(new
        {
            Offsets = new Dictionary<string, long>(),
            Messages = new Dictionary<string, LedgerEntry> { ["msg_old"] = new(At.ToUnixTimeSeconds(), 100, 20, 0, 0) },
        }));

        Assert.Equal(120, Summarize(ClaudeUsageLedger.Load(Path.Combine(_folder.Path, "projects"), store)).Total.Total);
    }

    [Fact]
    public void AChangedSourceIsReadWithoutRestartingAndACopiedResponseCountsOnce()
    {
        var first = WriteTranscript(Response("msg_1", input: 10, output: 5));
        IReadOnlyList<string> sources = [Path.Combine(_folder.Path, "projects")];
        var ledger = ClaudeUsageLedger.LoadSources(() => sources, Path.Combine(_folder.Path, "ledger.json"));
        Assert.Equal(15, Summarize(ledger).Total.Total);

        var moved = _folder.File("moved", "project", "session.jsonl");
        File.Copy(first, moved);
        File.AppendAllText(moved, Response("msg_2", input: 20, output: 10) + "\n");
        sources = [sources[0], Path.Combine(_folder.Path, "moved")];

        Assert.Equal(45, Summarize(ledger).Total.Total);
        File.Delete(first);
        Assert.Equal(45, Summarize(ledger).Total.Total);
    }

    [Fact]
    public void TheLedgerOutlivesItsTranscripts()
    {
        var transcript = WriteTranscript(Response("msg_1", input: 10, output: 5));
        var ledger = Ledger();
        ledger.Scan();
        ledger.Flush();

        File.Delete(transcript);
        var reopened = Ledger();

        Assert.Equal(15, Summarize(reopened).Total.Total);
    }

    private ClaudeUsageLedger Ledger() =>
        ClaudeUsageLedger.Load(Path.Combine(_folder.Path, "projects"), Path.Combine(_folder.Path, "ledger.json"));

    private static TokenSummary Summarize(ClaudeUsageLedger ledger)
    {
        ledger.Scan();
        return ledger.Summarize(At, At.AddHours(-5), At.AddDays(-7));
    }

    private string WriteTranscript(params string[] lines)
    {
        var transcript = _folder.File("projects", "C--code", "session.jsonl");
        File.WriteAllText(transcript, string.Join("\n", lines) + "\n");
        return transcript;
    }

    private static string Response(string id, long input, long output, long cacheRead = 0, long cacheWrite = 0) =>
        JsonSerializer.Serialize(new
        {
            type = "assistant",
            timestamp = At,
            message = new
            {
                id,
                usage = new
                {
                    input_tokens = input,
                    output_tokens = output,
                    cache_read_input_tokens = cacheRead,
                    cache_creation_input_tokens = cacheWrite,
                },
            },
        });
}
