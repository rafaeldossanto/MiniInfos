namespace MiniInfos.Tests;

/// <summary>The settings every card shares, and the startup shortcut the settings window turns on.</summary>
public sealed class PreferencesTests
{
    [Fact]
    public void WithNoFileTheCardsStayOnTopFollowTheAppsAndAreSimple()
    {
        using var folder = new TempFolder();

        var preferences = new Preferences(folder.File("settings.json"));

        Assert.Equal((true, true, ThemeChoice.Simple), (preferences.IsTopmost, preferences.FollowsApps, preferences.Theme));
    }

    [Fact]
    public void AChangeIsSavedAndToldOnce()
    {
        using var folder = new TempFolder();
        var path = folder.File("settings.json");
        var preferences = new Preferences(path);
        var told = new List<string?>();
        preferences.PropertyChanged += (_, e) => told.Add(e.PropertyName);

        preferences.Theme = ThemeChoice.Light;
        preferences.Theme = ThemeChoice.Light;
        preferences.IsTopmost = false;

        Assert.Equal([nameof(Preferences.Theme), nameof(Preferences.IsTopmost)], told);
        Assert.Contains("\"Theme\":\"Light\"", File.ReadAllText(path));
        var reloaded = new Preferences(path);
        Assert.Equal((false, true, ThemeChoice.Light), (reloaded.IsTopmost, reloaded.FollowsApps, reloaded.Theme));
    }

    [Fact]
    public void AnUnreadableFileReadsAsTheDefaults()
    {
        using var folder = new TempFolder();
        var path = folder.File("settings.json");
        File.WriteAllText(path, "{ not json");

        Assert.Equal(ThemeChoice.Simple, new Preferences(path).Theme);
    }

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
