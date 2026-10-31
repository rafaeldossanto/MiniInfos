using System.Text.Json;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Providers;
using MiniInfos.Providers.Claude;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Providers.Claude;

public sealed class WslClaudeHistoryTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task WindowsAndWslCopiesCountOnceAndAppendOnlyCompleteResponses()
    {
        var windows = _folder.File("windows", "session.jsonl");
        var linux = _folder.File("ubuntu", "session.jsonl");
        File.WriteAllText(windows, Response("shared", 10) + "\n");
        File.WriteAllText(linux, Response("shared", 10) + "\n" + Response("linux", 20) + "\n");
        var ledger = Ledger(Path.GetDirectoryName(windows)!);
        ledger.Scan();
        var history = new WslClaudeHistory(() => [Path.GetDirectoryName(linux)!], ledger.Import);

        await history.Refresh(force: true);
        Assert.Equal(30, Total(ledger));
        var conversation = Assert.Single(ledger.Sessions());
        Assert.Equal("linux-project", conversation.Source.Project);
        Assert.Equal(30, conversation.Tokens.Total);

        File.AppendAllText(linux, Response("appended", 7));
        await history.Refresh(force: true);
        Assert.Equal(30, Total(ledger));
        File.AppendAllText(linux, "\n");
        await history.Refresh(force: true);
        Assert.Equal(37, Total(ledger));

        ledger.Scan(); // A normal Windows refresh cannot make WSL replies disappear or count twice.
        await history.Refresh(force: true);
        Assert.Equal(37, Total(ledger));
    }

    [Fact]
    public async Task DistroDisappearanceRestartAndTruncationKeepPersistedTotalsWithoutDuplicates()
    {
        var file = _folder.File("ubuntu", "session.jsonl");
        File.WriteAllText(file, Response("first", 12) + "\n" + Response("second", 5) + "\n");
        var roots = new List<string> { Path.GetDirectoryName(file)! };
        var ledger = Ledger();
        var history = new WslClaudeHistory(() => roots, ledger.Import);
        await history.Refresh(force: true);
        roots.Clear();
        await history.Refresh(force: true);
        Assert.Equal(17, Total(ledger));
        ledger.Flush();

        var restored = Ledger();
        roots.Add(Path.GetDirectoryName(file)!);
        history = new(() => roots, restored.Import);
        await history.Refresh(force: true);
        Assert.Equal(17, Total(restored));
        File.WriteAllText(file, Response("new", 3) + "\n");
        await history.Refresh(force: true);
        Assert.Equal(20, Total(restored));
    }

    [Fact]
    public async Task AnUnavailableDistroCannotBlockWindowsCountsAndConcurrentRefreshesShareOneWorker()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var windows = _folder.File("windows", "session.jsonl");
        File.WriteAllText(windows, Response("windows", 8) + "\n");
        var ledger = Ledger(Path.GetDirectoryName(windows)!);
        var history = new WslClaudeHistory(() =>
        {
            entered.SetResult();
            release.Wait(Patience * 2);
            throw new IOException("Distro stopped during discovery.");
        }, ledger.Import);
        var pending = history.Refresh(force: true);
        try
        {
            await entered.Task.WaitAsync(Patience);
            Assert.Same(pending, history.Refresh(force: true));
            await Task.Run(ledger.Scan).WaitAsync(Patience);
            Assert.Equal(8, Total(ledger));
        }
        finally { release.Set(); }
        await pending.WaitAsync(Patience);
    }

    // The stalled distro holds a pool thread on purpose: under a loaded test run the pool may take seconds to add
    // the next one, so the deadlines only catch a refresh that truly waits for WSL.
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private ResponseLedger Ledger(params string[] roots) => new(() => roots, _folder.File("ledger.json"), "\"usage\""u8, ClaudeUsageLedger.Read);
    private long Total(ResponseLedger ledger) => ledger.Summarize(_now, _now.AddHours(-5), _now.AddDays(-7)).Total.Total;
    private string Response(string id, int tokens) => JsonSerializer.Serialize(new
    {
        type = "assistant", timestamp = _now, sessionId = "conversation", cwd = "/home/friend/linux-project",
        message = new { id, usage = new { input_tokens = tokens } },
    });
}
