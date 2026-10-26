using System.Text.Json;
using MiniInfos.Sources;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Sources;

public sealed class UsageSourceDiscoveryTests : IDisposable
{
    private readonly TempFolder _folder = new();
    public void Dispose() => _folder.Dispose();

    [Fact]
    public void ConfiguredHomesAndDefaultsRemainAvailableWithoutLogsYet()
    {
        var context = Context() with
        {
            ClaudeHomes = [Folder("private", "anthropic-data")],
            CodexHomes = [Folder("private", "openai-data")],
        };

        var found = UsageSourceDiscovery.Find(context);

        Assert.Contains(context.ClaudeHomes[0], found.ClaudeRoots);
        Assert.Contains(context.CodexHomes[0], found.CodexRoots);
        Assert.Contains(Path.Combine(context.UserProfile, ".claude"), found.ClaudeRoots);
        Assert.Contains(Path.Combine(context.UserProfile, ".codex"), found.CodexRoots);
    }

    [Fact]
    public void RenamedHomesOnAnotherDriveAreRecognizedByTheirContents()
    {
        var claude = Folder("drive", "tools", "assistant-a");
        var codex = Folder("drive", "tools", "assistant-b");
        Directory.CreateDirectory(Path.Combine(claude, "projects"));
        File.WriteAllText(Path.Combine(claude, "history.jsonl"), "");
        Directory.CreateDirectory(Path.Combine(codex, "sessions"));
        File.WriteAllText(Path.Combine(codex, "installation_id"), "");

        var found = UsageSourceDiscovery.Find(Context() with { SearchRoots = [Folder("drive")] });

        Assert.Contains(claude, found.ClaudeRoots);
        Assert.Contains(codex, found.CodexRoots);
        Assert.Contains(Path.Combine(codex, "archived_sessions"), found.CodexSessions);
    }

    [Fact]
    public void AnOrdinaryProjectWithLookalikeFoldersIsNotTakenForAnAssistant()
    {
        var project = Folder("drive", "dev", "website");
        Directory.CreateDirectory(Path.Combine(project, "projects"));
        Directory.CreateDirectory(Path.Combine(project, "sessions"));
        File.WriteAllText(Path.Combine(project, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(project, "config.toml"), "");

        var found = UsageSourceDiscovery.Find(Context() with { SearchRoots = [Folder("drive")] });

        Assert.DoesNotContain(project, found.ClaudeRoots);
        Assert.DoesNotContain(project, found.CodexRoots);
    }

    [Fact]
    public void StoreSandboxAndFoldersBesideAnAppAreSearched()
    {
        var context = Context();
        var packaged = Folder("local", "Packages", "OpenAI.Codex_example", "LocalCache", "Roaming", ".codex");
        Directory.CreateDirectory(Path.Combine(packaged, "archived_sessions"));
        var portable = Folder("portable", "data", "claude");
        Directory.CreateDirectory(Path.Combine(portable, "projects"));

        var found = UsageSourceDiscovery.Find(context with { AppFolders = [Folder("portable")] });

        Assert.Contains(packaged, found.CodexRoots);
        Assert.Contains(portable, found.ClaudeRoots);
    }

    [Fact]
    public void AClaudeChatCanRegisterAPrivateHomeOutsideTheSearchArea()
    {
        var root = Folder("outside", "deep", "custom");
        var marker = _folder.File("local", "MiniInfos", "sources", "claude-example.json");
        File.WriteAllText(marker, JsonSerializer.Serialize(new { Provider = "claude", Root = root }));

        var found = UsageSourceDiscovery.Find(Context());

        Assert.Contains(root, found.ClaudeRoots);
        Assert.Contains(Path.Combine(root, "miniinfos", "sessions"), found.ClaudeFeeds);
    }

    [Fact]
    public void ANewLocationIsFoundOnTheNextSearchAndRemembered()
    {
        var context = Context();
        var before = UsageSourceDiscovery.Find(context);
        var root = Folder("profile", "apps", "codex-portable");
        Directory.CreateDirectory(Path.Combine(root, "sessions"));

        var after = UsageSourceDiscovery.Find(context, before);
        var restored = UsageSourceDiscovery.Find(context with { UserProfile = Folder("another-profile") }, after);

        Assert.DoesNotContain(root, before.CodexRoots);
        Assert.Contains(root, after.CodexRoots);
        Assert.Contains(root, restored.CodexRoots);
    }

    [Fact]
    public void OverlappingSearchesDoNotRepeatRootsOrAcceptUnrelatedSessionFolders()
    {
        var root = Folder("profile", ".codex");
        Directory.CreateDirectory(Path.Combine(root, "sessions"));
        var unrelated = Folder("profile", "some-app", "sessions");
        var dependency = Folder("profile", "node_modules", ".codex", "sessions");

        var found = UsageSourceDiscovery.Find(Context() with { CodexHomes = [root, root.ToUpperInvariant()] });

        Assert.Single(found.CodexRoots, path => path.Equals(root, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(Path.GetDirectoryName(unrelated)!, found.CodexRoots);
        Assert.DoesNotContain(Path.GetDirectoryName(dependency)!, found.CodexRoots);
    }

    private SourceSearchContext Context() => new(Folder("profile"), Folder("local"), Folder("roaming"), [], [], [], []);

    private string Folder(params string[] parts)
    {
        var path = Path.Combine([_folder.Path, .. parts]);
        Directory.CreateDirectory(path);
        return path;
    }
}
