using System.IO;
using System.Windows.Threading;
using System.Windows;
using MiniInfos.Infrastructure.Windows;
using MiniInfos.Infrastructure;
using MiniInfos.Providers;
using MiniInfos.Providers.Claude;
using MiniInfos.Settings;
using MiniInfos.UI.Themes;
using MiniInfos.UI.Windows;
using MiniInfos.Updates;

namespace MiniInfos;

public partial class App : Application
{
    private const string SingleInstanceName = "MiniInfos.SingleInstance";
    private const string ShowSignalName = "MiniInfos.Show";
    private const string QuitSignalName = "MiniInfos.Quit";

    /// <summary>Ends the one that runs, as its "Sair" does: what the installer asks before it replaces the files.</summary>
    private const string QuitArgument = "--quit";

    /// <summary>The installer's: registers the Claude Code mod with whichever Claude Code the search finds, or takes it out.</summary>
    private const string InstallModArgument = "--install-mod";
    private const string RemoveModArgument = "--remove-mod";

    private static readonly TimeSpan WatchAppsEvery = TimeSpan.FromSeconds(3);

    private readonly DispatcherTimer _appWatch = new() { Interval = WatchAppsEvery };
    private readonly List<(EventWaitHandle Signal, RegisteredWaitHandle Wait)> _signals = [];
    private Mutex? _singleInstance;
    private TrayIcon? _trayIcon;
    private Updater? _updater;

    protected override void OnStartup(StartupEventArgs e)
    {
        // The startup shortcut's flag: hidden until an AI app opens.
        var startsHiddenWithApps = e.Args.Contains(StartupShortcut.Arguments);
        var quits = e.Args.Contains(QuitArgument);

        // A job for the installer, done without a window while the card itself may be closed: the exit code answers it.
        if (e.Args.Contains(InstallModArgument))
        {
            Shutdown(ClaudeMod.Default.Install(Path.Combine(AppContext.BaseDirectory, "claude-plugin")) ? 0 : 1);
            return;
        }

        if (e.Args.Contains(RemoveModArgument))
        {
            ClaudeMod.Default.Remove();
            Shutdown();
            return;
        }

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
        L.Follow(preferences);
        ThemeManager.Follow(preferences);
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

        // A newer version shows as a line above the card; taking it reinstalls silently and reopens the app.
        var http = GitHubReleases.CreateClient();
        _updater = new Updater(new GitHubReleases(http), http, Version.Parse(AppInfo.Version), Updater.RunInstaller(cards.Quit));
        _ = new UpdateBanner(cards.Main, _updater, preferences);
        _updater.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _appWatch.Stop();
        _updater?.Dispose();
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
