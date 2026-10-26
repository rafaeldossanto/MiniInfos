namespace MiniInfos.Tests;

public sealed class CodexCliTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void ACodexOnPathWins()
    {
        var onPath = Touch("bin", "codex.exe");
        Touch("local", "OpenAI", "Codex", "bin", "build", "codex.exe");

        Assert.Equal(onPath, CodexCli.Find(Folder("bin"), Folder("local")));
    }

    [Fact]
    public void NpmsBatchShimOnPathIsFound()
    {
        var shim = Touch("npm", "codex.cmd");

        Assert.Equal(shim, CodexCli.Find($"{Folder("empty")};{Folder("npm")}", Folder("local")));
    }

    [Fact]
    public void WithNoneOnPathTheDesktopAppsNewestCopyIsUsed()
    {
        // The Store app keeps codex here and puts no folder of it on PATH: the case of a real install.
        var old = Touch("local", "OpenAI", "Codex", "bin", "old", "codex.exe");
        var newest = Touch("local", "OpenAI", "Codex", "bin", "new", "codex.exe");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-3));

        Assert.Equal(newest, CodexCli.Find(Folder("empty"), Folder("local")));
    }

    [Fact]
    public void AStorePackagesOwnCopyIsFoundToo()
    {
        var packaged = Touch("local", "Packages", "OpenAI.Codex_2p2nqsd0c76g0", "LocalCache", "Local", "OpenAI", "Codex", "bin", "b1", "codex.exe");

        Assert.Equal(packaged, CodexCli.Find(null, Folder("local")));
    }

    [Fact]
    public void NoCodexAnywhereIsNull()
    {
        Assert.Null(CodexCli.Find($"{Folder("empty")};\"bad|entry\"", Folder("local")));
    }

    private string Folder(string name)
    {
        var folder = Path.Combine(_folder.Path, name);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private string Touch(params string[] parts)
    {
        var file = _folder.File(parts);
        File.WriteAllText(file, "");
        return file;
    }
}
