using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MiniInfos;

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
    public SettingsWindow(Preferences preferences, StartupShortcut startup, MainWindow card, Action quit)
    {
        _preferences = preferences;
        _startup = startup;
        _quit = quit;
        Card = card;
        InitializeComponent();
        DataContext = preferences;
        Owner = card;
        VersionText.Text = $"Versão {AppInfo.Version}";

        StartupToggle.IsChecked = startup.IsOn;
        ShowTheme();
        ShowNumbers();
        preferences.PropertyChanged += OnPreferenceChanged;
        card.ViewChanged += ShowNumbers;
        Closed += (_, _) =>
        {
            preferences.PropertyChanged -= OnPreferenceChanged;
            card.ViewChanged -= ShowNumbers;
        };

        PlaceOver(card);
    }

    public MainWindow Card { get; }

    /// <summary>Centered on the card, kept on screen.</summary>
    private void PlaceOver(MainWindow card)
    {
        var right = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth;
        var bottom = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
        Left = Math.Clamp(card.Left + (card.ActualWidth - Width) / 2, SystemParameters.VirtualScreenLeft + ScreenMargin, right - Width - ScreenMargin);
        Top = Math.Clamp(card.Top + (card.ActualHeight - Height) / 2, SystemParameters.VirtualScreenTop + ScreenMargin, bottom - Height - ScreenMargin);
    }

    private void ShowTheme()
    {
        foreach (var choice in (RadioButton[])[SimpleTheme, ClassicTheme, LightTheme, SystemTheme])
        {
            if ((ThemeChoice)choice.Tag == _preferences.Theme) choice.IsChecked = true;
        }
    }

    private void ShowNumbers() => (Card.IsDetailed ? DetailedNumbers : CompactNumbers).IsChecked = true;

    private void OnPreferenceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Preferences.Theme)) ShowTheme();
    }

    private void OnThemeChosen(object sender, RoutedEventArgs e) => _preferences.Theme = (ThemeChoice)((RadioButton)sender).Tag;

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
