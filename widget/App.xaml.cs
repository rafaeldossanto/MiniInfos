using System.Windows;
using System.Windows.Threading;

namespace MiniInfos;

public partial class App : Application
{
    private const string SingleInstanceName = "MiniInfos.SingleInstance";
    private const string ShowSignalName = "MiniInfos.Show";
    private const string QuitSignalName = "MiniInfos.Quit";

    /// <summary>Legacy startup flag: hidden until Claude or Codex opens.</summary>
    private const string WithClaudeArgument = "--with-claude";

    /// <summary>Ends the one that runs, as its "Sair" does: what the installer asks before it replaces the files.</summary>
    private const string QuitArgument = "--quit";

    private static readonly TimeSpan WatchAppsEvery = TimeSpan.FromSeconds(3);

    private readonly DispatcherTimer _appWatch = new() { Interval = WatchAppsEvery };
    private readonly List<(EventWaitHandle Signal, RegisteredWaitHandle Wait)> _signals = [];
    private Mutex? _singleInstance;
    private TrayIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        var startsHiddenWithApps = e.Args.Contains(WithClaudeArgument);
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
        var providers = UsageProviders.Create();
        var window = new MainWindow(providers);
        MainWindow = window;
        _trayIcon = new TrayIcon(window);
        if (!startsHiddenWithApps) window.Show();

        var follower = new AssistantFollower(() => providers.Any(provider => provider.IsInUse()), window.ShowWithAssistant, window.HideCard);
        follower.Check();
        _appWatch.Tick += (_, _) => follower.Check();
        _appWatch.Start();

        Listen(ShowSignalName, window.BringBack);
        Listen(QuitSignalName, window.Quit);
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
