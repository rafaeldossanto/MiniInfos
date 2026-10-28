using MiniInfos.Providers.Claude;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Providers.Claude;

public sealed class ClaudeModTests : IDisposable
{
    private const string Uninstall = "plugin uninstall miniinfos@miniinfos --scope user";
    private const string RemoveMarketplace = "plugin marketplace remove miniinfos";

    private readonly TempFolder _folder = new();
    private readonly List<(string Claude, string Arguments)> _ran = [];
    public void Dispose() => _folder.Dispose();

    private string Remembered => Path.Combine(_folder.Path, "data", "claude-code.txt");

    [Fact]
    public void EachClaudeCodeFoundIsTriedUntilOneTakesTheModAndThatOneIsRemembered()
    {
        var refuses = Claude("refuses");
        var takes = Claude("takes");

        Assert.True(Mod([refuses, takes], claude => claude == refuses ? 1 : 0).Install(@"C:\MiniInfos\claude-plugin"));

        Assert.Equal(
            [(takes, Uninstall), (takes, RemoveMarketplace), (takes, "plugin marketplace add \"C:\\MiniInfos\\claude-plugin\""),
                (takes, "plugin install miniinfos@miniinfos --scope user")],
            _ran.Where(run => run.Claude == takes));
        Assert.Equal(takes, File.ReadAllText(Remembered));
    }

    [Fact]
    public void TheOneThatWorkedLastSparesTheSearch()
    {
        var remembered = Claude("remembered");
        Directory.CreateDirectory(Path.GetDirectoryName(Remembered)!);
        File.WriteAllText(Remembered, remembered);
        var searched = false;

        Assert.True(new ClaudeMod(Remembered, () => { searched = true; return [Claude("other")]; }, Run(_ => 0)).Install("plugin"));

        Assert.False(searched);
        Assert.All(_ran, run => Assert.Equal(remembered, run.Claude));
    }

    [Fact]
    public void ARememberedClaudeCodeThatIsGoneLeavesItToTheSearch()
    {
        var gone = Claude("gone");
        Directory.CreateDirectory(Path.GetDirectoryName(Remembered)!);
        File.WriteAllText(Remembered, gone);
        File.Delete(gone);
        var found = Claude("found");

        Assert.True(Mod([found], _ => 0).Install("plugin"));

        Assert.DoesNotContain(_ran, run => run.Claude == gone);
        Assert.Equal(found, File.ReadAllText(Remembered));
    }

    [Fact]
    public void WithoutAClaudeCodeThatTakesItTheModIsMissing()
    {
        Assert.False(Mod([Claude("refuses")], _ => 1).Install("plugin"));
        Assert.False(Mod([], _ => 0).Install("plugin"));
        Assert.False(File.Exists(Remembered));
    }

    [Fact]
    public void RemovingStopsAtTheFirstClaudeCodeThatRuns()
    {
        var broken = Claude("broken");
        var runs = Claude("runs");
        var never = Claude("never");

        // "Not installed" is an answer too: that Claude Code can still take the marketplace out.
        Mod([broken, runs, never], claude => claude == broken ? -1 : 1).Remove();

        Assert.Equal([(runs, Uninstall), (runs, RemoveMarketplace)], _ran.Where(run => run.Claude != broken));
    }

    private ClaudeMod Mod(IReadOnlyList<string> found, Func<string, int> exitCode) => new(Remembered, () => found, Run(exitCode));

    private Func<string, string, int> Run(Func<string, int> exitCode) => (claude, arguments) =>
    {
        _ran.Add((claude, arguments));
        return exitCode(claude);
    };

    private string Claude(string folder)
    {
        var path = _folder.File(folder, "claude.cmd");
        File.WriteAllText(path, ClaudeCodeTests.NpmShim);
        return path;
    }
}
