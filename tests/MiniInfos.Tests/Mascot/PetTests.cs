using System.Windows;
using MiniInfos.Mascot;
using MiniInfos.Providers;
using MiniInfos.Settings;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Mascot;

public sealed class PetTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-25T18:00:00Z");

    [Fact]
    public void HistoricalTokensDoNotLookLikeNewActivity()
    {
        var pet = new PetActivity();
        pet.Observe([new("claude", 900_000, 20)], Now);
        Assert.Equal(PetState.Idle, pet.StateAt(Now));
        Assert.Equal(PetState.Sleep, pet.StateAt(Now + PetActivity.SleepAfter));
    }

    [Fact]
    public void TokenIncreasesWakeThePetAndThenItSettlesAndSleeps()
    {
        var pet = new PetActivity();
        pet.Observe([new("codex", 100, null)], Now);
        var later = Now + PetActivity.SleepAfter;
        pet.Observe([new("codex", 105, null)], later);
        Assert.Equal(PetState.Active, pet.StateAt(later));
        Assert.Equal(PetState.Idle, pet.StateAt(later + PetActivity.ActiveFor));
        Assert.Equal(PetState.Sleep, pet.StateAt(later + PetActivity.SleepAfter));
    }

    [Fact]
    public void EachProviderIsTrackedSeparatelyAndADecreasingLedgerDoesNotWakeIt()
    {
        var pet = new PetActivity();
        pet.Observe([new("claude", 200, null), new("codex", 300, null)], Now);
        var later = Now + PetActivity.SleepAfter;
        pet.Observe([new("claude", 190, null), new("codex", 300, null)], later);
        Assert.Equal(PetState.Sleep, pet.StateAt(later));
        pet.Observe([new("claude", 180, null), new("codex", 301, null)], later);
        Assert.Equal(PetState.Active, pet.StateAt(later));
    }

    [Fact]
    public void ACurrentNearLimitReadingHasPriorityUntilTheLimitClears()
    {
        var pet = new PetActivity();
        pet.Observe([new("codex", 100, 90)], Now);
        pet.Observe([new("codex", 101, 90)], Now);
        Assert.Equal(PetState.Alert, pet.StateAt(Now));
        pet.Observe([new("codex", 100, 3)], Now + PetActivity.SleepAfter);
        Assert.Equal(PetState.Sleep, pet.StateAt(Now + PetActivity.SleepAfter));
    }

    [Fact]
    public void ExpiredLimitsAreNotWarnings()
    {
        var past = new WindowLine(new PlanWindow(TimeSpan.FromHours(5), new("5h", 99, Now.AddSeconds(-1).ToString("O"))), default);
        var current = new WindowLine(new PlanWindow(TimeSpan.FromDays(7), new("week", 32, Now.AddDays(1).ToString("O"))), default);
        var page = new UsagePage("Codex", past, current, default, new(50, 0, 0, 0), "local", "", "", "", "");
        var usage = PetUsage.Of("codex", page, Now);
        Assert.Equal(32, usage.LimitPercent);
        Assert.Equal(50, usage.Total);
    }

    [Fact]
    public void PetPreferencesAreSavedAndRemainIndependentOfFontsAndTheme()
    {
        using var folder = new TempFolder();
        var path = folder.File("settings.json");
        var preferences = new Preferences(path) { PetEnabled = true, PetSize = PetSizeChoice.Large };
        preferences.Theme = ThemeChoice.Classic;
        preferences.NumberFont = FontChoice.Orbitron;
        var reloaded = new Preferences(path);
        Assert.True(reloaded.PetEnabled);
        Assert.Equal(PetSizeChoice.Large, reloaded.PetSize);
        var defaults = new Preferences(folder.File("missing.json"));
        Assert.False(defaults.PetEnabled);
        Assert.Equal(PetSizeChoice.Medium, defaults.PetSize);
    }

    [Fact]
    public void SavedPositionRemainsReachableAfterAMonitorIsRemoved()
    {
        var primary = new Rect(0, 0, 1920, 1080);
        Assert.Equal(new PetPlacement(0, 920), new PetPlacement(-1700, 3000).KeepVisible(primary, 160));
        Assert.Equal(new PetPlacement(0, 0), new PetPlacement(double.NaN, double.PositiveInfinity).KeepVisible(primary, 160));
        Assert.Equal(new PetPlacement(-100, 0), new PetPlacement(-4000, -5).KeepVisible(new Rect(-100, 0, 80, 80), 160));
    }
}
