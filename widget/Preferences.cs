using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace MiniInfos;

/// <summary>The cards' colors: one of the dictionaries in Themes\, or the one Windows' own mode picks.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ThemeChoice>))]
public enum ThemeChoice
{
    /// <summary>The plain dark card, with blue bars.</summary>
    Simple,

    /// <summary>The dark, illustrated Athena edition of the card, in stone tones.</summary>
    Classic,

    Light,

    /// <summary>Simple or Light as Windows' app mode is, changing along with it.</summary>
    System,
}

/// <summary>What every card shares, as settings.json keeps it.</summary>
/// <param name="IsTopmost">The cards stay in front of other windows.</param>
/// <param name="FollowsApps">The cards show when Claude or Codex opens and hide after the last one closes.</param>
public sealed record AppSettings(bool IsTopmost = true, bool FollowsApps = true, ThemeChoice Theme = ThemeChoice.Simple);

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
        set => Change(_settings with { Theme = value });
    }

    private void Change(AppSettings next, [CallerMemberName] string property = "")
    {
        if (next == _settings) return;

        _settings = next;
        _file.Save(next);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}
