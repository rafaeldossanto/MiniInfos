namespace MiniInfos.Tests.Providers.Codex;

public sealed class CodexCliUnixTests
{
    [Fact]
    public void RunningUnixCliIsFoundOutsidePathWithoutStartingAnAppBundleAsAServer()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "MiniInfos-cli-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(root, "Codex.app", "Contents", "MacOS", "Codex");
        var cli = Path.Combine(root, "moved cli", "codex");
        try
        {
            foreach (var path in new[] { app, cli })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, "#!/bin/sh\nexit 0\n");
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            }
            Assert.Equal(cli, MiniInfos.Providers.Codex.CodexCli.Find("", root, [app, cli]));
            Assert.Null(MiniInfos.Providers.Codex.CodexCli.Find("", root, [app]));
        }
        finally
        {
            File.Delete(app); File.Delete(cli);
            Directory.Delete(Path.GetDirectoryName(app)!); Directory.Delete(Path.Combine(root, "Codex.app", "Contents"));
            Directory.Delete(Path.Combine(root, "Codex.app")); Directory.Delete(Path.GetDirectoryName(cli)!); Directory.Delete(root);
        }
    }

}
