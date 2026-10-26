using System.Windows;
using System.IO;
using Microsoft.Win32;
using MiniInfos.Community;
using MiniInfos.Settings;

namespace MiniInfos.UI.Themes;

/// <summary>
/// Dresses every window in the chosen theme: each theme is a dictionary of the same brushes, and the
/// windows take them as dynamic resources, so swapping the dictionary recolors everything at once.
/// </summary>
public static class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static ResourceDictionary? _applied;

    /// <summary>The resource dictionary has changed; cards can measure their new header height.</summary>
    public static event Action? Changed;

    /// <summary>Applies the chosen theme now and again whenever it changes, or Windows' mode does under <see cref="ThemeChoice.System"/>.</summary>
    public static void Follow(Preferences preferences)
    {
        Apply(preferences);
        preferences.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Preferences.Theme) or nameof(Preferences.CommunityThemeId)) Apply(preferences);
        };
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && preferences.Theme == ThemeChoice.System)
            {
                Application.Current.Dispatcher.InvokeAsync(() => Apply(preferences));
            }
        };
    }

    /// <summary>The brushes of a theme: <see cref="ThemeChoice.System"/> reads as the one Windows is in.</summary>
    public static ResourceDictionary Load(ThemeChoice theme)
    {
        var resources = new ResourceDictionary();
        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/MiniInfos;component/UI/Themes/Design.xaml", UriKind.Absolute),
        });
        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/MiniInfos;component/UI/Themes/{Resolve(theme)}.xaml", UriKind.Absolute),
        });
        return resources;
    }

    public static ResourceDictionary Load(Preferences preferences)
    {
        var package = PackageLibrary.Shared.Find(PackageKind.Theme, preferences.CommunityThemeId);
        if (package is not null)
        {
            try { return CommunityTheme.Load(package); }
            catch (Exception e) when (e is PackageException or IOException or UnauthorizedAccessException) { }
        }
        return Load(preferences.Theme);
    }

    public static ThemeChoice Resolve(ThemeChoice theme) => theme switch
    {
        ThemeChoice.System => IsWindowsLight() ? ThemeChoice.Light : ThemeChoice.Simple,
        _ => theme,
    };

    private static void Apply(Preferences preferences)
    {
        var next = Load(preferences);
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        if (_applied is not null) dictionaries.Remove(_applied);
        dictionaries.Add(next);
        _applied = next;
        Changed?.Invoke();
    }

    private static bool IsWindowsLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
    }
}
