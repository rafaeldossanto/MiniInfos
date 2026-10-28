using MiniInfos.Providers.Claude;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Providers.Claude;

public sealed class ClaudeCodeTests : IDisposable
{
    // What npm lays as claude.cmd: a script that starts the package.
    internal const string NpmShim = "@ECHO off\r\n\"%_prog%\" \"%dp0%\\node_modules\\@anthropic-ai\\claude-code\\cli.js\" %*\r\n";

    private readonly TempFolder _folder = new();
    public void Dispose() => _folder.Dispose();

    [Fact]
    public void TheScanReachesClaudeCodeInHiddenFoldersAndLeavesLookalikesOut()
    {
        var hidden = _folder.File("Users", "friend", "AppData", "Roaming", "npm", "claude.cmd");
        File.WriteAllText(hidden, NpmShim);
        File.SetAttributes(Path.Combine(_folder.Path, "Users", "friend", "AppData"), FileAttributes.Directory | FileAttributes.Hidden);
        File.WriteAllText(_folder.File("tools", "claude.cmd"), "@echo another claude");
        // The desktop app's own claude.exe does not call itself Claude Code.
        File.WriteAllBytes(_folder.File("Claude", "claude.exe"), [0x4D, 0x5A]);
        File.WriteAllText(_folder.File("notes", "claude.txt"), NpmShim);

        Assert.Equal([hidden], ClaudeCode.Find([_folder.Path], path: ""));
    }

    [Fact]
    public void ThePathOneComesFirstAndCountsOnce()
    {
        var onPath = _folder.File("bin", "claude.cmd");
        var elsewhere = _folder.File("old", "claude.cmd");
        File.WriteAllText(onPath, NpmShim);
        File.WriteAllText(elsewhere, NpmShim);

        Assert.Equal([onPath, elsewhere], ClaudeCode.Find([_folder.Path], Path.GetDirectoryName(onPath)));
    }

    [Fact]
    public void TheUsersFolderGoesFirstAndTheDriveAfterItLeavesItOut()
    {
        var drive = _folder.File("Program Files", "Claude", "claude.cmd");
        var user = _folder.File("Users", "friend", "npm", "claude.cmd");
        File.WriteAllText(drive, NpmShim);
        File.WriteAllText(user, NpmShim);

        Assert.Equal([user, drive], ClaudeCode.Find([Path.Combine(_folder.Path, "Users", "friend"), _folder.Path], path: ""));
    }

    [Fact]
    public void NothingFoundIsNothing() =>
        Assert.Empty(ClaudeCode.Find([_folder.Path, Path.Combine(_folder.Path, "missing")], path: ""));
}
