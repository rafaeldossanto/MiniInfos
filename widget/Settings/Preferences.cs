using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Mascot;

namespace MiniInfos.Settings;

/// <summary>What every card shares, as settings.json keeps it.</summary>
/// <param name="IsTopmost">The cards stay in front of other windows.</param>
/// <param name="FollowsApps">The cards show when Claude or Codex opens and hide after the last one closes.</param>
public sealed record AppSettings(
    bool IsTopmost = true,
    bool FollowsApps = true,
    ThemeChoice Theme = ThemeChoice.Simple,
    FontChoice TextFont = FontChoice.Manrope,
    FontChoice NumberFont = FontChoice.Manrope,
    bool PetEnabled = false,
    PetSizeChoice PetSize = PetSizeChoice.Medium,
    string? CommunityThemeId = null,
    string? PetId = null,
    IReadOnlyList<string>? RecentThemes = null,
    LanguageChoice Language = LanguageChoice.Automatic);

/// <summary>
/// The settings every card shares, saved the moment they change. The cards and the settings window
/// bind to them, so a change made in one place shows in all.
/// </summary>
public sealed class Preferences : INotifyPropertyChanged
{
    private readonly JsonFile<AppSettings> _file;
    private AppSettings _settings;

    public Preferences(string path)
    {
        _file = new JsonFile<AppSettings>(path);
        _settings = _file.Load() ?? new AppSettings();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public LanguageChoice Language
    {
        get => Enum.IsDefined(_settings.Language) ? _settings.Language : LanguageChoice.Automatic;
        set => Change(_settings with { Language = Enum.IsDefined(value) ? value : LanguageChoice.Automatic });
    }

    /// <summary>The app's own settings, in its data folder.</summary>
    public static Preferences Load() => new(Path.Combine(AppPaths.Data, "settings.json"));

    public bool IsTopmost
    {
        get => _settings.IsTopmost;
        set => Change(_settings with { IsTopmost = value });
    }

    public bool FollowsApps
    {
        get => _settings.FollowsApps;
        set => Change(_settings with { FollowsApps = value });
    }

    public ThemeChoice Theme
    {
        get => _settings.Theme;
        set
        {
            var wasCustom = _settings.CommunityThemeId is not null;
            Change(_settings with { Theme = value, CommunityThemeId = null, RecentThemes = ThemeHistory.Promote(_settings.RecentThemes, ThemeHistory.Key(value)) });
            if (wasCustom) RefreshTheme();
        }
    }

    public FontChoice TextFont
    {
        get => _settings.TextFont;
        set => Change(_settings with { TextFont = value });
    }

    public FontChoice NumberFont
    {
        get => _settings.NumberFont;
        set => Change(_settings with { NumberFont = value });
    }

    public bool PetEnabled
    {
        get => _settings.PetEnabled;
        set => Change(_settings with { PetEnabled = value });
    }

    public PetSizeChoice PetSize
    {
        get => _settings.PetSize;
        set => Change(_settings with { PetSize = value });
    }

    public string? CommunityThemeId
    {
        get => _settings.CommunityThemeId;
        set
        {
            var id = string.IsNullOrWhiteSpace(value) ? null : value;
            // Choosing a community theme puts it first in the row; clearing one (removed, or a built-in chosen) changes no order.
            Change(_settings with { CommunityThemeId = id, RecentThemes = id is null ? _settings.RecentThemes : ThemeHistory.Promote(_settings.RecentThemes, id) });
        }
    }

    /// <summary>
    /// Theme keys (<see cref="ThemeHistory"/>), the last chosen first. The theme in use always leads, even one
    /// chosen before the history was kept.
    /// </summary>
    public IReadOnlyList<string> RecentThemes => ThemeHistory.Promote(_settings.RecentThemes ?? [], ThemeKey);

    /// <summary>The theme in use, as <see cref="ThemeHistory"/> keys it.</summary>
    public string ThemeKey => CommunityThemeId ?? ThemeHistory.Key(Theme);

    /// <summary>Uses the theme a <see cref="ThemeHistory"/> key names: a built-in, or a community package's id.</summary>
    public void UseTheme(string key)
    {
        if (ThemeHistory.BuiltIn(key) is { } theme) Theme = theme;
        else CommunityThemeId = key;
    }

    public string? PetId
    {
        get => _settings.PetId;
        set => Change(_settings with { PetId = string.IsNullOrWhiteSpace(value) ? null : value });
    }

    public void RefreshTheme() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CommunityThemeId)));
    public void RefreshPet() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PetId)));

    private void Change(AppSettings next, [CallerMemberName] string property = "")
    {
        if (next == _settings) return;

        _settings = next;
        _file.Save(next);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}
