using System.Windows;

namespace MiniInfos;

/// <summary>
/// The cards on screen: the main one, whose numbers the tray icon reads, and those opened beside it
/// with "Nova janela", which come back with the app. Showing, hiding and quitting go to them all.
/// </summary>
public sealed class CardSet
{
    private readonly IReadOnlyList<IUsageProvider> _providers;
    private readonly List<MainWindow> _extras = [];

    public CardSet(IReadOnlyList<IUsageProvider> providers)
    {
        _providers = providers;
        Main = Adopt(new MainWindow(providers));
        foreach (var store in CardStore.Extras()) _extras.Add(Adopt(new MainWindow(providers, store, CardKind.Extra)));
        LightOpenPages();
    }

    public MainWindow Main { get; }

    public IReadOnlyList<MainWindow> Extras => _extras;

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

    /// <summary>Quits the app with every card saved as it is, so the extras come back next time.</summary>
    public void Quit()
    {
        foreach (var card in All) card.PrepareToQuit();
        Application.Current.Shutdown();
    }

    private MainWindow Adopt(MainWindow card)
    {
        card.NewWindowRequested += OpenNew;
        card.QuitRequested += Quit;
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
        var card = Adopt(new MainWindow(_providers, CardStore.NewExtra(from.SettingsForNewCard(providerId)), CardKind.Extra));
        _extras.Add(card);
        card.BringBack();
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
