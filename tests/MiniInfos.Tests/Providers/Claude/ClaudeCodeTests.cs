using MiniInfos.Providers.Claude;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Providers.Claude;

public sealed class ClaudeCodeTests : IDisposable
{
    // What npm lays as claude.cmd: a script that starts the package.
    internal const string NpmShim = "@ECHO off\r\n\"%_prog%\" \"%dp0%\\node_modules\\@anthropic-ai\\claude-code\\cli.js\" %*\r\n";

    private static readonly string Name = OperatingSystem.IsWindows() ? "claude.cmd" : "claude";

    private readonly TempFolder _folder = new();
    public void Dispose() => _folder.Dispose();

    [Fact]
    public void TheScanReachesClaudeCodeInHiddenFoldersAndLeavesLookalikesOut()
    {
        var hidden = ClaudeCodeIn("Users", "friend", "AppData", "Roaming", "npm");
        if (OperatingSystem.IsWindows())
            File.SetAttributes(Path.Combine(_folder.Path, "Users", "friend", "AppData"), FileAttributes.Directory | FileAttributes.Hidden);
        // Named like it, but another script on Windows and one that cannot run elsewhere.
        File.WriteAllText(_folder.File("tools", Name), "@echo another claude");
        // The desktop app's own claude.exe does not call itself Claude Code.
        File.WriteAllBytes(_folder.File("Claude", "claude.exe"), [0x4D, 0x5A]);
        File.WriteAllText(_folder.File("notes", "claude.txt"), NpmShim);

        Assert.Equal([hidden], ClaudeCode.Find([_folder.Path], path: ""));
    }

    [Fact]
    public void ThePathOneComesFirstAndCountsOnce()
    {
        var onPath = ClaudeCodeIn("bin");
        var elsewhere = ClaudeCodeIn("old");

        Assert.Equal([onPath, elsewhere], ClaudeCode.Find([_folder.Path], Path.GetDirectoryName(onPath)));
    }

    [Fact]
    public void TheUsersFolderGoesFirstAndTheDriveAfterItLeavesItOut()
    {
        var drive = ClaudeCodeIn("Program Files", "Claude");
        var user = ClaudeCodeIn("Users", "friend", "npm");

        Assert.Equal([user, drive], ClaudeCode.Find([Path.Combine(_folder.Path, "Users", "friend"), _folder.Path], path: ""));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheDesktopCopyIsFoundWithoutPathAndWithoutAssumingItsVersionLayout(bool packaged)
    {
        var root = packaged
            ? Path.Combine("AppData", "Local", "Packages", "Claude_example", "LocalCache", "Roaming", "Claude", "claude-code")
            : Path.Combine("AppData", "Roaming", "Claude", "claude-code");
        var versioned = ClaudeCodeIn(root, "2.1.304", "build-id");
        var changedLayout = ClaudeCodeIn(root, "new-version", "native", "runtime", "build-id");

        var found = ClaudeCode.Find([_folder.Path], path: "").ToArray();

        Assert.Equal(2, found.Length);
        Assert.Contains(versioned, found);
        Assert.Contains(changedLayout, found);
    }

    [Fact]
    public void NothingFoundIsNothing() =>
        Assert.Empty(ClaudeCode.Find([_folder.Path, Path.Combine(_folder.Path, "missing")], path: ""));

    /// <summary>Claude Code as each system lays it: npm's claude.cmd on Windows, an executable claude elsewhere.</summary>
    private string ClaudeCodeIn(params string[] folders)
    {
        var path = _folder.File([.. folders, Name]);
        File.WriteAllText(path, NpmShim);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }
}
