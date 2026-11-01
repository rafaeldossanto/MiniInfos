using MiniInfos.Infrastructure.Desktop;
using MiniInfos.Providers.Claude;
using MiniInfos.Sources;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Sources;

public sealed class WslClaudeSourcesTests : IDisposable
{
    private readonly TempFolder _folder = new();
    public void Dispose() => _folder.Dispose();

    [Fact]
    public void RunningListAcceptsUnicodeAndSpacesButRejectsPathsAndDocker()
    {
        var names = WslDistributions.Parse("\uFEFFUbuntu\r\nDebian\nubuntu\nMy Linux\nLinux-ação\n..\n../escape\nC:\\escape\n--bad/argument\ndocker-desktop\ndocker-desktop-data\n");
        Assert.Equal(["Ubuntu", "Debian", "My Linux", "Linux-ação"], names);
        Assert.Equal(["Ubuntu"], WslDistributions.Parse("U\0b\0u\0n\0t\0u\0\r\0\n\0"));
    }

    [Fact]
    public void OnlyRunningDistributionsAreProbedAndOnlyTheirClaudeProjectsAreReturned()
    {
        var ubuntu = Project("Ubuntu", "home", "friend");
        var root = Project("Ubuntu", "root");
        var debian = Project("Debian", "home", "other");
        Project("Stopped", "home", "friend");
        var probed = new List<string>();

        var found = WslClaudeSources.Projects(() => ["Ubuntu", "Debian", "Ubuntu"], name =>
        {
            probed.Add(name);
            return Path.Combine(_folder.Path, name);
        });

        Assert.Equal(["Ubuntu", "Debian"], probed);
        Assert.Equal(3, found.Count);
        Assert.Contains(ubuntu, found);
        Assert.Contains(root, found);
        Assert.Contains(debian, found);
    }

    [Fact]
    public void AStoppedOrMissingDistroDoesNotBecomeAPersistedSourceOrRequireAProbe()
    {
        var active = new List<string> { "Ubuntu" };
        var expected = Project("Ubuntu", "home", "friend");
        string Root(string name) => Path.Combine(_folder.Path, name);
        Assert.Equal([expected], WslClaudeSources.Projects(() => active, Root));

        active.Clear();
        Assert.Empty(WslClaudeSources.Projects(() => active, _ => throw new InvalidOperationException("No stopped distro may be probed.")));
        Assert.Empty(WslClaudeSources.Projects(() => ["Gone", "../escape"], Root));

        active.Add("Ubuntu");
        Assert.Equal([expected], WslClaudeSources.Projects(() => active, Root));
    }

    [Theory]
    [InlineData(@"\\wsl.localhost\Ubuntu\home\friend\.claude\projects\project\session.jsonl")]
    [InlineData(@"\\WSL$\Ubuntu\home\friend\.claude\projects\project\session.jsonl")]
    public void ConversationTitlesNeverReopenRememberedWslPaths(string path)
    {
        Assert.True(WslDistributions.IsPath(path));
        Assert.Null(ChatTitles.Custom(path, "session"));
    }

    private string Project(string distribution, params string[] home)
    {
        var path = Path.Combine([_folder.Path, distribution, .. home, ".claude", "projects"]);
        Directory.CreateDirectory(path);
        return path;
    }
}
