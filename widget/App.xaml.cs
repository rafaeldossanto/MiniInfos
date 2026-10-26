using System.Windows;
using System.Windows.Threading;

namespace MiniInfos;

public partial class App : Application
{
    private const string SingleInstanceName = "MiniInfos.SingleInstance";
    private const string ShowSignalName = "MiniInfos.Show";
    private const string QuitSignalName = "MiniInfos.Quit";

    /// <summary>Ends the one that runs, as its "Sair" does: what the installer asks before it replaces the files.</summary>
    private const string QuitArgument = "--quit";

    private static readonly TimeSpan WatchAppsEvery = TimeSpan.FromSeconds(3);

    private readonly DispatcherTimer _appWatch = new() { Interval = WatchAppsEvery };
    private readonly List<(EventWaitHandle Signal, RegisteredWaitHandle Wait)> _signals = [];
    private Mutex? _singleInstance;
    private TrayIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        // The startup shortcut's flag: hidden until Claude or Codex opens.
        var startsHiddenWithApps = e.Args.Contains(StartupShortcut.Arguments);
        var quits = e.Args.Contains(QuitArgument);

        _singleInstance = new Mutex(initiallyOwned: true, SingleInstanceName, out var isFirstInstance);
        if (!isFirstInstance || quits)
        {
            // A second start speaks to the one that runs: quit it, or bring it back when opened by hand.
            if (quits) Raise(QuitSignalName);
            else if (!startsHiddenWithApps) Raise(ShowSignalName);

            Shutdown();
            return;
        }

        base.OnStartup(e);
        var preferences = Preferences.Load();
        Themes.Follow(preferences);
        AppFonts.Follow(preferences);
        var providers = UsageProviders.Create();
        var cards = new CardSet(providers, preferences);
        MainWindow = cards.Main;
        _trayIcon = new TrayIcon(cards);

        // Not following the apps, nothing would ever show a card started hidden: it shows now.
        if (!startsHiddenWithApps || !preferences.FollowsApps) cards.ShowWithAssistant();

        var follower = new AssistantFollower(() => providers.Any(provider => provider.IsInUse()), cards.ShowWithAssistant, cards.Hide)
        {
            IsEnabled = preferences.FollowsApps,
        };
        preferences.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(Preferences.FollowsApps)) follower.IsEnabled = preferences.FollowsApps;
        };
        follower.Check();
        _appWatch.Tick += (_, _) => follower.Check();
        _appWatch.Start();

        Listen(ShowSignalName, cards.BringBack);
        Listen(QuitSignalName, cards.Quit);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _appWatch.Stop();
        _trayIcon?.Dispose();
        foreach (var (signal, wait) in _signals)
        {
            wait.Unregister(null);
            signal.Dispose();
        }

        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private static void Raise(string name)
    {
        if (!EventWaitHandle.TryOpenExisting(name, out var signal)) return;

        using (signal) signal.Set();
    }

    private void Listen(string name, Action onSignal)
    {
        var signal = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, name);
        var wait = ThreadPool.RegisterWaitForSingleObject(
            signal,
            (_, _) => Dispatcher.Invoke(onSignal),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
        _signals.Add((signal, wait));
    }
}
