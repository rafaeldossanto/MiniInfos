using System.Windows;
using System.Windows.Threading;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Infrastructure.Windows;
using MiniInfos.Mascot;
using MiniInfos.Providers;
using MiniInfos.Settings;
using MiniInfos.Community.Catalog;

namespace MiniInfos.UI.Windows;

/// <summary>
/// The cards on screen: the main one, whose numbers the tray icon reads, and those opened beside it
/// with "Nova janela", which come back with the app. Showing, hiding and quitting go to them all, and
/// they share one settings window.
/// </summary>
public sealed class CardSet
{
    /// <summary>Apps are installed and removed rarely, and looking costs little; ↻ looks at once.</summary>
    private static readonly TimeSpan CheckInstallsEvery = TimeSpan.FromMinutes(1);

    private readonly InstalledProviders _providers;
    private readonly Preferences _preferences;
    private readonly List<MainWindow> _extras = [];
    private readonly DesktopPet _pet;
    private readonly CatalogBrowser? _catalog;
    private readonly DispatcherTimer _installWatch = new() { Interval = CheckInstallsEvery };

    /// <param name="providers">Every provider: each card pages through those whose app is installed.</param>
    public CardSet(IReadOnlyList<IUsageProvider> providers, Preferences preferences, CatalogBrowser? catalog = null)
    {
        _providers = new InstalledProviders(providers);
        _preferences = preferences;
        _catalog = catalog;
        Main = Adopt(new MainWindow(_providers.Shown, preferences));
        _pet = new DesktopPet(preferences, BringBack, OpenPetSettings, Quit);
        Main.UsageRead += _pet.Observe;
        foreach (var store in CardStore.Extras()) _extras.Add(Adopt(new MainWindow(_providers.Shown, preferences, store, CardKind.Extra)));
        LightOpenPages();

        _providers.Changed += ShowInstalledProviders;
        _installWatch.Tick += (_, _) => _ = CheckInstallsAsync();
        _installWatch.Start();
    }

    public MainWindow Main { get; }

    /// <summary>The providers with a page, in every card.</summary>
    public IReadOnlyList<IUsageProvider> Providers => _providers.Shown;

    public IReadOnlyList<MainWindow> Extras => _extras;

    /// <summary>The settings window, while it is open.</summary>
    public SettingsWindow? Settings { get; private set; }

    public PetWindow? Pet => _pet.Window;

    public bool IsVisible => All.Any(card => card.IsVisible);

    private IEnumerable<MainWindow> All => [Main, .. _extras];

    /// <summary>Shows every card without taking the keyboard from the app that just opened.</summary>
    public void ShowWithAssistant()
    {
        foreach (var card in All) card.ShowWithAssistant();
    }

    /// <summary>Brings every card back, the main one last so it ends up in front.</summary>
    public void BringBack()
    {
        foreach (var card in _extras) card.ShowWithAssistant();
        Main.BringBack();
    }

    public void Hide()
    {
        foreach (var card in All) card.HideCard();
    }

    public Task ForceRefreshAsync() => Task.WhenAll(All.Select(card => card.ForceRefreshAsync()));

    /// <summary>
    /// Looks again at which apps are installed: a page comes or goes with its app, in every card. It starts
    /// from the cards' dispatcher, so the pages change on their thread whoever asks.
    /// </summary>
    public Task CheckInstallsAsync() => Main.Dispatcher.InvokeAsync(_providers.CheckAsync).Task.Unwrap();

    /// <summary>
    /// Opens the settings for a card, shown first since the window stays above it. One window at a
    /// time: asked again for the same card, it comes to the front; for another, it reopens for that one.
    /// </summary>
    public void OpenSettings(MainWindow card)
    {
        if (Settings is { } open)
        {
            if (open.Card == card)
            {
                open.Activate();
                return;
            }

            open.Close();
        }

        card.BringBack();
        var settings = new SettingsWindow(_preferences, StartupShortcut.Default, card, Quit, _catalog);
        settings.Closed += (_, _) =>
        {
            if (Settings == settings) Settings = null;
        };
        Settings = settings;
        settings.Show();
    }

    /// <summary>Quits the app with every card saved as it is, so the extras come back next time.</summary>
    public void Quit()
    {
        _installWatch.Stop();
        Main.UsageRead -= _pet.Observe;
        _pet.Dispose();
        foreach (var card in All) card.PrepareToQuit();
        Application.Current.Shutdown();
    }

    private void OpenPetSettings()
    {
        OpenSettings(Main);
        Settings?.ShowPetPage();
    }

    private MainWindow Adopt(MainWindow card)
    {
        card.NewWindowRequested += OpenNew;
        card.QuitRequested += Quit;
        card.SettingsRequested += OpenSettings;
        card.RefreshForced += () => _ = CheckInstallsAsync();
        card.PagesChanged += LightOpenPages;
        card.IsVisibleChanged += (_, _) => LightOpenPages();
        card.Dismissed += closed =>
        {
            _extras.Remove(closed);
            LightOpenPages();
        };
        return card;
    }

    private void OpenNew(MainWindow from, string providerId)
    {
        var card = Adopt(new MainWindow(_providers.Shown, _preferences, CardStore.NewExtra(from.SettingsForNewCard(providerId)), CardKind.Extra));
        _extras.Add(card);
        card.BringBack();
        LightOpenPages();
    }

    private void ShowInstalledProviders()
    {
        foreach (var card in All) card.ShowProviders(_providers.Shown);
        LightOpenPages();
    }

    /// <summary>
    /// Every card's pager lights the pages open on screen, in it or in another visible card: two cards
    /// side by side read as one split card. A hidden card's pages are not on screen and don't count.
    /// </summary>
    private void LightOpenPages()
    {
        foreach (var card in All)
        {
            var elsewhere = All.Where(other => other != card && other.IsVisible).SelectMany(other => other.ShownPages).ToHashSet();
            card.LightPagesShownElsewhere(elsewhere);
        }
    }
}
