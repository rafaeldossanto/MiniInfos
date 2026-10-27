using System.Text.Json;
using MiniInfos.Providers;
using MiniInfos.Providers.Kimi;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Providers.Kimi;

public sealed class KimiUsageTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Now;

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void EveryAgentsTurnsCountOnceWithTheirCacheApart()
    {
        WriteWire("main",
            Line(new { type = "llm.request", model = "kimi-code", turnStep = "1.1" }),
            Usage(inputOther: 6_631, output: 206, cacheRead: 12_800),
            Usage(inputOther: 2_222, output: 131, cacheRead: 19_200, cacheWrite: 40, offsetMs: 6_000));
        WriteWire("subagent-1", Usage(inputOther: 500, output: 50, offsetMs: 3_000));

        var tokens = Summarize(Ledger()).Total;

        Assert.Equal(new TokenCount(6_631 + 2_222 + 500, 206 + 131 + 50, 12_800 + 19_200, 40), tokens);
    }

    [Fact]
    public void LinesThatAreNotUsageRecordsAreLeftOut()
    {
        WriteWire("main",
            Line(new { type = "context.append_message", text = "\"usage.record\" quoted in a chat" }),
            "not json \"usage.record\"",
            Usage(inputOther: 7, output: 3));

        Assert.Equal(10, Summarize(Ledger()).Total.Total);
    }

    [Fact]
    public void TheLedgerOutlivesItsSessions()
    {
        var wire = WriteWire("main", Usage(inputOther: 10, output: 5));
        var ledger = Ledger();
        ledger.Scan();
        ledger.Flush();

        File.Delete(wire);

        Assert.Equal(15, Summarize(Ledger()).Total.Total);
    }

    [Fact]
    public async Task AKimiCodeSessionShowsItsProjectAndNoLimits()
    {
        WriteWire("main", Usage(inputOther: 10, output: 5));
        File.WriteAllText(_folder.File("sessions", "wd_abc", "session_1", "state.json"),
            JsonSerializer.Serialize(new { workDir = "C:/code/my-project", title = "private prompt" }));

        var page = await new KimiUsage(Sessions, Store, () => 22).ReadAsync(At, isShown: true, force: false);

        Assert.Equal("my-project", page.Activity);
        Assert.Null(page.ShortWindow.Window.Limit);
        Assert.Null(page.LongWindow.Window.Limit);
        Assert.Equal(15, page.Today.Total);
    }

    [Fact]
    public void TheDesktopContextIsTheLatestConversations()
    {
        var path = _folder.File("conversation-context-usage.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["agent:main:main:conversation:old"] = new { contextTokens = 200_000, maxContextTokens = 262_144, updatedAt = At.AddHours(-1) },
            ["agent:main:main:conversation:new"] = new { contextTokens = 57_557, maxContextTokens = 262_144, updatedAt = At },
            ["broken"] = new { contextTokens = 1, maxContextTokens = 0, updatedAt = At.AddHours(1) },
        }));

        Assert.Equal(21.96, KimiContext.LatestPercent(path)!.Value, precision: 2);
        Assert.Null(KimiContext.LatestPercent(_folder.File("missing.json")));
    }

    private string Store => Path.Combine(_folder.Path, "kimi-ledger.json");

    private IReadOnlyList<string> Sessions() => [Path.Combine(_folder.Path, "sessions")];

    private ResponseLedger Ledger() => new(Sessions, Store, KimiResponses.Marker, KimiResponses.Read);

    private static TokenSummary Summarize(ResponseLedger ledger)
    {
        ledger.Scan();
        return ledger.Summarize(At, At.AddHours(-5), At.AddDays(-7));
    }

    private string WriteWire(string agent, params string[] lines)
    {
        var wire = _folder.File("sessions", "wd_abc", "session_1", "agents", agent, "wire.jsonl");
        File.WriteAllText(wire, string.Join("\n", lines) + "\n");
        return wire;
    }

    private static string Usage(long inputOther, long output, long cacheRead = 0, long cacheWrite = 0, long offsetMs = 0) => Line(new
    {
        type = "usage.record",
        model = "kimi-code",
        usage = new { inputOther, output, inputCacheRead = cacheRead, inputCacheCreation = cacheWrite },
        usageScope = "turn",
        time = At.ToUnixTimeMilliseconds() + offsetMs,
    });

    private static string Line(object value) => JsonSerializer.Serialize(value);
}
