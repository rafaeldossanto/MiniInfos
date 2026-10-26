using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using MiniInfos.Community;
using MiniInfos.Settings;
using MiniInfos.UI.Controls;
using MiniInfos.UI.Themes;

namespace MiniInfos.UI.Windows;

/// <summary>
/// "Biblioteca" on the Aparência page: every theme there is to choose, built-in and installed, each on a card with its
/// preview, the last chosen first. The cards keep the order they opened in, so the one clicked does not move away.
/// </summary>
public partial class ThemeLibraryWindow : DialogWindow
{
    private readonly Preferences _preferences;
    private readonly Action _showCommunity;
    private readonly List<(string Key, Border Card, Button Use)> _cards = [];

    /// <param name="showCommunity">Opens the Comunidade page on its themes.</param>
    public ThemeLibraryWindow(Preferences preferences, Action showCommunity)
    {
        _preferences = preferences;
        _showCommunity = showCommunity;
        InitializeComponent();
        preferences.PropertyChanged += OnPreferenceChanged;
        Closed += (_, _) => preferences.PropertyChanged -= OnPreferenceChanged;
        ShowThemes();
    }

    /// <summary>Every card has its preview, or none when its package can no longer be read.</summary>
    public Task Ready { get; private set; } = Task.CompletedTask;

    /// <summary>Lays the cards out again, in the order of now: a theme was installed or removed.</summary>
    public void ShowThemes()
    {
        Tiles.Children.Clear();
        _cards.Clear();
        var drawing = new List<Task>();
        foreach (var option in ThemeOption.Available(_preferences, PackageLibrary.Shared))
        {
            var preview = PackageCard.Preview();
            var use = new Button { Tag = option.Key };
            use.Click += (_, _) => _preferences.UseTheme(option.Key);
            var card = PackageCard.Create(this, preview, option.Name, [option.Origin], use);
            AutomationProperties.SetName(card, option.Name);
            Tiles.Children.Add(card);
            _cards.Add((option.Key, card, use));
            // Drawn once the window shows, one at a time: a community theme reads its artwork from disk.
            drawing.Add(Dispatcher.InvokeAsync(() => Draw(option, preview), DispatcherPriority.Background).Task);
        }
        ShowInUse();
        Ready = Task.WhenAll(drawing);
    }

    private static void Draw(ThemeOption option, Image preview)
    {
        try { preview.Source = PackagePreviews.Theme(option.Resources()); }
        // The card stays: choosing it falls back to a built-in, as the cards themselves do.
        catch (Exception e) when (CommunityTheme.IsUnreadable(e)) { }
    }

    /// <summary>The theme in use reads "Em uso" and is outlined; the others offer Usar.</summary>
    private void ShowInUse()
    {
        foreach (var (key, card, use) in _cards)
        {
            var inUse = key == _preferences.ThemeKey;
            use.Content = inUse ? "Em uso" : "Usar";
            use.IsEnabled = !inUse;
            card.SetResourceReference(Border.BorderBrushProperty, inUse ? "SelectedBorder" : "Divider");
        }
    }

    private void OnPreferenceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Preferences.Theme) or nameof(Preferences.CommunityThemeId)) ShowInUse();
    }

    private void OnShowCommunity(object sender, RoutedEventArgs e)
    {
        _showCommunity();
        Close();
    }
}
