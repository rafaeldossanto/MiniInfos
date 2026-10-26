using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MiniInfos.Community;
using MiniInfos.UI.Themes;

namespace MiniInfos.UI.Windows;

/// <summary>
/// Tema on the Aparência page: the row holds the last chosen themes, the one in use first, built-in and community
/// alike; Biblioteca opens all of them with their previews.
/// </summary>
public partial class SettingsWindow
{
    /// <summary>What fits the row beside the page's margins; the others wait in the library.</summary>
    public const int ThemeRowSize = 4;

    /// <summary>The widest a theme's name gets in the row before it trims; the tooltip and the library show it whole.</summary>
    private const double ThemeNameWidth = 96;

    /// <summary>Lays the row out in the order of now: choosing a theme moves it to the front.</summary>
    private void ShowTheme()
    {
        var focused = ThemeChoices.Children.OfType<RadioButton>().FirstOrDefault(choice => choice.IsKeyboardFocused)?.Tag;
        ThemeChoices.Children.Clear();
        foreach (var option in ThemeOption.Available(_preferences, PackageLibrary.Shared).Take(ThemeRowSize))
        {
            var choice = ThemeButton(option);
            ThemeChoices.Children.Add(choice);
            if (Equals(option.Key, focused)) choice.Focus();
        }
    }

    private RadioButton ThemeButton(ThemeOption option)
    {
        var choice = new RadioButton
        {
            Content = new TextBlock { Text = option.Name, MaxWidth = ThemeNameWidth, TextTrimming = TextTrimming.CharacterEllipsis },
            Tag = option.Key, GroupName = "Theme", Style = (Style)FindResource("Segment"), ToolTip = option.Origin,
            IsChecked = option.Key == _preferences.ThemeKey,
        };
        AutomationProperties.SetName(choice, option.Name);
        // Subscribed after IsChecked, so laying the row out chooses nothing.
        choice.Checked += (_, _) => _preferences.UseTheme(option.Key);
        return choice;
    }

    private void OnShowThemeLibrary(object sender, RoutedEventArgs e) =>
        ShowOwned(() => new ThemeLibraryWindow(_preferences, ShowCommunityThemes));
}
