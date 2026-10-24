using System.Windows;
using System.Windows.Threading;

namespace MiniInfos;

public partial class App : Application
{
    private const string SingleInstanceName = "MiniInfos.SingleInstance";
    private const string ShowSignalName = "MiniInfos.Show";
    private const string QuitSignalName = "MiniInfos.Quit";

    /// <summary>How Windows starts it at sign-in: hidden, until Claude opens.</summary>
    private const string WithClaudeArgument = "--with-claude";

    /// <summary>Ends the one that runs, as its "Sair" does: what the installer asks before it replaces the files.</summary>
    private const string QuitArgument = "--quit";

    private static readonly TimeSpan WatchClaudeEvery = TimeSpan.FromSeconds(3);

    private readonly DispatcherTimer _claudeWatch = new() { Interval = WatchClaudeEvery };
    private readonly List<(EventWaitHandle Signal, RegisteredWaitHandle Wait)> _signals = [];
    private Mutex? _singleInstance;
    private TrayIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        var startsWithClaude = e.Args.Contains(WithClaudeArgument);
        var quits = e.Args.Contains(QuitArgument);

        _singleInstance = new Mutex(initiallyOwned: true, SingleInstanceName, out var isFirstInstance);
        if (!isFirstInstance || quits)
        {
            // A second start speaks to the one that runs: quit it, or bring it back when opened by hand.
            if (quits) Raise(QuitSignalName);
            else if (!startsWithClaude) Raise(ShowSignalName);

            Shutdown();
            return;
        }

        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        _trayIcon = new TrayIcon(window);
        if (!startsWithClaude) window.Show();

        var follower = new ClaudeFollower(IsClaudeOpen, window.ShowWithClaude, window.HideCard);
        follower.Check();
        _claudeWatch.Tick += (_, _) => follower.Check();
        _claudeWatch.Start();

        Listen(ShowSignalName, window.BringBack);
        Listen(QuitSignalName, window.Quit);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _claudeWatch.Stop();
        _trayIcon?.Dispose();
        foreach (var (signal, wait) in _signals)
        {
            wait.Unregister(null);
            signal.Dispose();
        }

        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    /// <summary>The desktop app's window, or any Claude Code chat still running: in a terminal, an editor or the app.</summary>
    private static bool IsClaudeOpen() => ClaudeApp.IsOpen() || FeedReader.Read(DateTimeOffset.Now).HasOpenChat;

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
