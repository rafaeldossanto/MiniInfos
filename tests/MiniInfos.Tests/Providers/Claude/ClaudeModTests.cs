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
    public void TheInstalledCopyIsAvailableForDetectionWithoutSearchingOrRunningACommand()
    {
        var code = Claude("outside-path");
        var mod = Mod([code], _ => 0);
        Assert.True(mod.Install("plugin"));
        var calls = _ran.Count;
        var restored = new ClaudeMod(Remembered, () => throw new InvalidOperationException("Detection must not scan the drives."),
            (_, _) => throw new InvalidOperationException("Detection must not run commands."));

        Assert.Equal(code, restored.InstalledCode);
        Assert.Equal(calls, _ran.Count);
        File.Delete(code);
        Assert.Null(restored.InstalledCode);
    }

    [Fact]
    public void AnUnrelatedFileAtTheRememberedPathDoesNotKeepClaudeInstalled()
    {
        var code = Claude("replaced");
        var mod = Mod([code], _ => 0);
        Assert.True(mod.Install("plugin"));
        File.WriteAllText(code, "@echo another program");

        Assert.Null(mod.InstalledCode);
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
    public void ARefusedModDoesNotHideAnInstalledClaudeCodeFromTokenTracking()
    {
        var code = Claude("refuses");
        var mod = Mod([code], _ => 1);

        Assert.False(mod.Install("plugin"));
        Assert.Equal(code, mod.InstalledCode);
        Assert.Equal(code, File.ReadAllText(Remembered));
    }

    [Fact]
    public void WithoutAClaudeCodeTheModIsMissing()
    {
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
