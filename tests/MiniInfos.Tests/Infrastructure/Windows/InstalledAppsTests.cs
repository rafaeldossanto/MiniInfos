using MiniInfos.Infrastructure.Desktop;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Infrastructure.Windows;

public sealed class InstalledAppsTests : IDisposable
{
    private const string Extension = "publisher.tool";
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void ARegisteredStorePackageCounts()
    {
        var app = new AppFootprint { StorePackages = ["Publisher.Tool_abc123"] };

        Assert.True(InstalledApps.Has(app, Context(hasStorePackage: family => family == "Publisher.Tool_abc123")));
        Assert.False(InstalledApps.Has(app, Context()));
    }

    [Fact]
    public void APackageWindowsNeverHeardOfIsNotInstalled() =>
        Assert.False(InstalledApps.HasStorePackage("MiniInfos.NoSuchPackage_0000000000000"));

    [Fact]
    public void ACommandOnPathCountsAsTheExeOrTheCmdNpmLays()
    {
        var bin = Path.GetDirectoryName(_folder.File("bin", "tool.cmd"))!;
        File.WriteAllText(Path.Combine(bin, "tool.cmd"), "");
        var app = new AppFootprint { Commands = ["tool"] };

        Assert.True(InstalledApps.Has(app, Context(path: $"C:\\nowhere;\"{bin}\"")));
        Assert.False(InstalledApps.Has(app, Context(path: "C:\\nowhere")));
    }

    [Fact]
    public void AFileAnInstallerLaysCounts()
    {
        var exe = _folder.File("Programs", "tool.exe");
        var app = new AppFootprint { Files = [exe] };
        Assert.False(InstalledApps.Has(app, Context()));

        File.WriteAllText(exe, "");
        Assert.True(InstalledApps.Has(app, Context()));
    }

    [Fact]
    public void AnEditorExtensionCountsInVsCodeOrAFork()
    {
        Directory.CreateDirectory(Path.Combine(_folder.Path, ".cursor", "extensions", $"{Extension}-1.2.3-win32-x64"));

        Assert.True(InstalledApps.Has(new AppFootprint { EditorExtensions = [Extension] }, Context()));
        Assert.False(InstalledApps.Has(new AppFootprint { EditorExtensions = ["publisher.other"] }, Context()));
    }

    [Fact]
    public void AnExtensionUninstalledButNotYetDeletedNoLongerCounts()
    {
        var extensions = Path.Combine(_folder.Path, ".vscode", "extensions");
        Directory.CreateDirectory(Path.Combine(extensions, $"{Extension}-1.2.3"));
        File.WriteAllText(Path.Combine(extensions, ".obsolete"), $"{{\"{Extension}-1.2.3\":true}}");

        Assert.False(InstalledApps.Has(new AppFootprint { EditorExtensions = [Extension] }, Context()));
    }

    [Fact]
    public void ARunningProcessCountsHoweverItWasInstalled()
    {
        var app = new AppFootprint { Processes = ["tool"] };

        Assert.True(InstalledApps.Has(app, Context(isRunning: name => name == "tool")));
        Assert.False(InstalledApps.Has(app, Context()));
    }

    [Fact]
    public void WithNoneOfItsSignsAnAppIsNotInstalled()
    {
        // Its data folder is no sign: it stays after an uninstall.
        Directory.CreateDirectory(Path.Combine(_folder.Path, ".tool", "sessions"));
        var app = new AppFootprint
        {
            StorePackages = ["Publisher.Tool_abc123"],
            Commands = ["tool"],
            EditorExtensions = [Extension],
            Files = [Path.Combine(_folder.Path, "Programs", "tool.exe")],
            Processes = ["tool"],
        };

        Assert.False(InstalledApps.Has(app, Context(path: _folder.Path)));
    }

    private InstallContext Context(string path = "", Func<string, bool>? hasStorePackage = null, Func<string, bool>? isRunning = null) =>
        new(path, _folder.Path, hasStorePackage ?? (_ => false), isRunning ?? (_ => false));
}
