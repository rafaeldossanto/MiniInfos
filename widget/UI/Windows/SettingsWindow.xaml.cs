using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using MiniInfos.Infrastructure.Windows;
using MiniInfos.Infrastructure;
using MiniInfos.Community.Catalog;
using MiniInfos.Mascot;
using MiniInfos.Settings;
using MiniInfos.UI.Controls;
using MiniInfos.UI.Themes;

namespace MiniInfos.UI.Windows;

/// <summary>
/// The settings, opened from a card's gear: Geral and the theme are shared by every card; "Este cartão"
/// acts on the card it was opened from.
/// </summary>
public partial class SettingsWindow : Window
{
    private const double ScreenMargin = 8;

    private readonly Preferences _preferences;
    private readonly StartupShortcut _startup;
    private readonly Action _quit;

    /// <param name="card">The card whose gear opened it: it stays above it, and closes with it.</param>
    /// <param name="quit">Quits the app, every card saved.</param>
    public SettingsWindow(Preferences preferences, StartupShortcut startup, ISettingsCard card, Action quit, CatalogBrowser? catalog = null)
    {
        _preferences = preferences;
        _startup = startup;
        _quit = quit;
        _catalogBrowser = catalog ?? CatalogBrowser.CreateDefault();
        Card = card;
        InitializeComponent();
        DataContext = preferences;
        Owner = card.Window;
        VersionText.Text = L.Format("Versão {0}", AppInfo.Version);

        StartupToggle.IsChecked = startup.IsOn;
        BuildFontChoices(TextFontChoices, AppFonts.TextOptions, "TextFont", choice => _preferences.TextFont = choice);
        BuildFontChoices(NumberFontChoices, AppFonts.NumberOptions, "NumberFont", choice => _preferences.NumberFont = choice);
        ShowFonts();
        InitializeCommunity();
        InitializeCatalog();
        ShowPetSize();
        ShowNumbers();
        ShowLanguage();
        ShowMainCard();
        L.Current.Changed += OnLanguageChanged;
        preferences.PropertyChanged += OnPreferenceChanged;
        card.ViewChanged += ShowNumbers;
        Closed += (_, _) =>
        {
            L.Current.Changed -= OnLanguageChanged;
            preferences.PropertyChanged -= OnPreferenceChanged;
            card.ViewChanged -= ShowNumbers;
        };

        PlaceOver(card.Window);
    }

    public ISettingsCard Card { get; }

    public void ShowPetPage() => Pages.SelectedItem = PetPage;

    /// <summary>Centered on the card, kept on screen.</summary>
    private void PlaceOver(Window card)
    {
        var right = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth;
        var bottom = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
        Left = Math.Clamp(card.Left + (card.ActualWidth - Width) / 2, SystemParameters.VirtualScreenLeft + ScreenMargin, right - Width - ScreenMargin);
        Top = Math.Clamp(card.Top + (card.ActualHeight - Height) / 2, SystemParameters.VirtualScreenTop + ScreenMargin, bottom - Height - ScreenMargin);
    }

    private void ShowNumbers() => (Card.IsDetailed ? DetailedNumbers : CompactNumbers).IsChecked = true;

    private void BuildFontChoices(StackPanel panel, IReadOnlyList<FontOption> options, string group, Action<FontChoice> choose)
    {
        foreach (var option in options)
        {
            var button = new RadioButton
            {
                Content = option.Name,
                Tag = option.Choice,
                GroupName = group,
                Style = (Style)FindResource("Segment"),
                FontFamily = option.Family,
            };
            button.Checked += (_, _) => choose(option.Choice);
            panel.Children.Add(button);
        }
    }

    /// <summary>Lights the chosen fonts and writes the preview in them, capitals included.</summary>
    private void ShowFonts()
    {
        foreach (var (panel, selected) in new[]
            { (TextFontChoices, _preferences.TextFont), (NumberFontChoices, _preferences.NumberFont) })
        {
            foreach (var choice in panel.Children.OfType<RadioButton>())
                choice.IsChecked = (FontChoice)choice.Tag == selected;
        }

        NumericText.Show(FontPreview, $"MiniInfos · {Formatting.Format.Full(12_345_678)} · {Formatting.Format.Percent(42)}");
    }

    private void OnPreferenceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Preferences.Theme)) ShowTheme();
        if (e.PropertyName == nameof(Preferences.CommunityThemeId)) { ShowTheme(); ShowCommunitySelection(); }
        if (e.PropertyName == nameof(Preferences.PetId)) { ShowCommunitySelection(); ShowPetPreview(); }
        if (e.PropertyName is nameof(Preferences.TextFont) or nameof(Preferences.NumberFont)) ShowFonts();
        if (e.PropertyName == nameof(Preferences.PetSize)) ShowPetSize();
    }

    private void ShowPetSize()
    {
        foreach (var choice in (RadioButton[])[SmallPet, MediumPet, LargePet])
            choice.IsChecked = (PetSizeChoice)choice.Tag == _preferences.PetSize;
    }

    private void OnPetSizeChosen(object sender, RoutedEventArgs e) =>
        _preferences.PetSize = (PetSizeChoice)((RadioButton)sender).Tag;

    private void OnNumbersChosen(object sender, RoutedEventArgs e) => Card.IsDetailed = DetailedNumbers.IsChecked == true;

    /// <summary>Turns the startup shortcut on or off, then shows what the folder really holds, in case Windows refused.</summary>
    private void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        _startup.IsOn = StartupToggle.IsChecked == true;
        StartupToggle.IsChecked = _startup.IsOn;
    }

    private void OnBackToCorner(object sender, RoutedEventArgs e) => Card.BackToCorner();

    private void OnDefaultSize(object sender, RoutedEventArgs e) => Card.ResetSize();

    private void OnQuit(object sender, RoutedEventArgs e) => _quit();

    /// <summary>One window of a kind above the settings: asked again, the open one comes forward. It closes with them.</summary>
    private void ShowOwned<T>(Func<T> create) where T : Window
    {
        if (OwnedWindows.OfType<T>().FirstOrDefault() is { } open)
        {
            open.Activate();
            return;
        }

        var window = create();
        window.Owner = this;
        window.Show();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnTitlePressed(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        Close();
        e.Handled = true;
    }
}
