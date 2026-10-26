using MiniInfos.Infrastructure.Windows;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Infrastructure.Windows;

public sealed class StartupShortcutTests
{
    [Fact]
    public void TheStartupShortcutComesAndGoes()
    {
        using var folder = new TempFolder();
        var startup = new StartupShortcut(folder.Path);
        var shortcut = Path.Combine(folder.Path, "MiniInfos.lnk");

        startup.IsOn = true;
        Assert.True(startup.IsOn && File.Exists(shortcut));

        startup.IsOn = false;
        Assert.False(startup.IsOn || File.Exists(shortcut));
    }
}
