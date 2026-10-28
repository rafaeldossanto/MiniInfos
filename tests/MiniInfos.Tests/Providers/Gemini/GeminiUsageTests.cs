using System.Text.Json;
using MiniInfos.Providers;
using MiniInfos.Providers.Gemini;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Providers.Gemini;

public sealed class GeminiUsageTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Now;

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void AResponseAppendedAgainWithItsTokensCountsOnceWithCacheAndThoughtsApart()
    {
        WriteChat("session-a",
            Line(new { sessionId = "a", projectHash = "h", startTime = At }),
            Line(new { id = "u1", timestamp = At, type = "user", content = "hi" }),
            Line(new { id = "g1", timestamp = At, type = "gemini", content = "", model = "gemini-2.5-pro" }),
            Response("g1", input: 1_000, output: 200, cached: 600, thoughts: 50, tool: 30));

        var tokens = Summarize(Ledger()).Total;

        // Gemini's input holds its cached part; tool prompts are input, thoughts are output.
        Assert.Equal(new TokenCount(1_000 - 600 + 30, 200 + 50, 600, 0), tokens);
        Assert.Equal(1_000 + 200 + 50 + 30, tokens.Total);
    }

    [Fact]
    public void ARewoundResponseStillCountsAndUserLinesAreLeftOut()
    {
        WriteChat("session-a",
            Response("g1", input: 100, output: 10),
            Line(new Dictionary<string, string> { ["$rewindTo"] = "g1" }),
            Line(new { id = "u2", timestamp = At, type = "user", tokens = new { input = 999 } }),
            "not json \"tokens\"");

        Assert.Equal(110, Summarize(Ledger()).Total.Total);
    }

    [Fact]
    public void TheSameMessageIdInAnotherSessionCountsApart()
    {
        WriteChat("session-a", Response("g1", input: 100, output: 10));
        WriteChat("session-b", Response("g1", input: 200, output: 20));

        Assert.Equal(330, Summarize(Ledger()).Total.Total);
    }

    [Fact]
    public async Task ThePageNamesTheLatestModelAndHasNoLimits()
    {
        WriteChat("session-a",
            Response("g1", input: 100, output: 10, model: "gemini-2.5-flash", offsetSeconds: -60),
            Response("g2", input: 100, output: 10, model: "gemini-2.5-pro"));

        var page = await new GeminiUsage(Chats, Store).ReadAsync(At, isShown: true, force: false);

        Assert.Equal("gemini-2.5-pro", page.Activity);
        Assert.Null(page.ShortWindow.Window.Limit);
        Assert.Equal(220, page.Total.Total);
    }

    [Fact]
    public async Task EachChatIsAConversationWithItsSubagentsAndNamedByItsProject()
    {
        WriteChat("session-a", Response("g1", input: 100, output: 10));
        File.WriteAllText(_folder.File("tmp", "project", "chats", "session-a", "helper.jsonl"), Response("s1", input: 50, output: 5) + "\n");
        File.WriteAllText(_folder.File("tmp", new string('a', 64), "chats", "session-b.jsonl"), Response("g2", input: 1, output: 1) + "\n");
        var gemini = new GeminiUsage(Chats, Store);
        await gemini.ReadAsync(At, isShown: true, force: false);

        var chats = gemini.Conversations().ToDictionary(chat => chat.Key);

        Assert.Equal(("project", 165L), (chats["session-a"].Title, chats["session-a"].Tokens.Total));
        Assert.Equal(("Gemini CLI", 2L), (chats["session-b"].Title, chats["session-b"].Tokens.Total));
    }

    private string Store => Path.Combine(_folder.Path, "gemini-ledger.json");

    private IReadOnlyList<string> Chats() => [Path.Combine(_folder.Path, "tmp")];

    private ResponseLedger Ledger() => new(Chats, Store, GeminiResponses.Marker, GeminiResponses.Read);

    private static TokenSummary Summarize(ResponseLedger ledger)
    {
        ledger.Scan();
        return ledger.Summarize(At, At.AddHours(-5), At.AddDays(-7));
    }

    private void WriteChat(string session, params string[] lines) =>
        File.WriteAllText(_folder.File("tmp", "project", "chats", session + ".jsonl"), string.Join("\n", lines) + "\n");

    private static string Response(string id, long input, long output, long cached = 0, long thoughts = 0, long tool = 0,
        string model = "gemini-2.5-pro", int offsetSeconds = 0) => Line(new
    {
        id,
        timestamp = At.AddSeconds(offsetSeconds),
        type = "gemini",
        content = "answer",
        model,
        tokens = new { input, output, cached, thoughts, tool, total = input + output + thoughts + tool },
    });

    private static string Line(object value) => JsonSerializer.Serialize(value);
}
