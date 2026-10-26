namespace MiniInfos.UiCheck;

/// <summary>Runs independent visual scenarios. Focused runs use synthetic usage data and never contact the providers.</summary>
internal static partial class Program
{
    private const int WmNcHitTest = 0x0084;
    private const int WmSizing = 0x0214;
    private const int WmMouseHorizontalWheel = 0x020E;
    private const int HtLeft = 10;
    private const int HtBottom = 15;
    private const int HtBottomRight = 17;
    private const int HtClient = 1;
    private static int _failures;
    private static int _checks;
    private static bool _quiet;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--help"))
        {
            Console.WriteLine("MiniInfos.UiCheck [output-folder] [--suite Cards,Settings,Themes,Fonts,Mascot,Updates,Providers,Community|All] [--quiet]");
            return 0;
        }
        CheckOptions options;
        try { options = CheckOptions.Parse(args); }
        catch (ArgumentException exception) { Console.Error.WriteLine(exception.Message); return 2; }
        _quiet = options.Quiet;
        var data = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MiniInfos.UiCheck-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("MINIINFOS_DATA_DIR", data);
        Environment.SetEnvironmentVariable("MINIINFOS_STARTUP_DIR", System.IO.Path.Combine(data, "startup"));
        var pictures = options.Output ?? data;
        Directory.CreateDirectory(pictures);
        try { Run(pictures, options); }
        finally
        {
            // Explicit pictures survive; only this run's exact temporary data folder is removed.
            var temporaryRoot = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()));
            if (options.Output is not null && Directory.Exists(data)
                && System.IO.Path.GetDirectoryName(data)?.Equals(temporaryRoot, StringComparison.OrdinalIgnoreCase) == true
                && System.IO.Path.GetFileName(data).StartsWith("MiniInfos.UiCheck-", StringComparison.Ordinal))
                Directory.Delete(data, recursive: true);
        }
        Console.WriteLine(_failures == 0 ? $"ALL OK ({_checks} checks)" : $"{_failures} FAILED ({_checks} checks)");
        return _failures == 0 ? 0 : 1;
    }

    private static void Run(string pictures, CheckOptions options)
    {
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        // The app's UI thread always has its dispatcher's context, so an awaited click handler comes back to it. The checks
        // run between pumps, where WPF leaves none: without this, a suite run alone (Updates) resumed on the thread pool.
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var preferences = Preferences.Load();
        ThemeManager.Follow(preferences);
        AppFonts.Follow(preferences);
        var live = options.Includes("Providers");
        IReadOnlyList<IUsageProvider> providers = live ? UsageProviders.Create()
            : [new SampleProvider("claude", "Claude"), new SampleProvider("codex", "Codex (GPT)")];
        var cards = new CardSet(providers, preferences, EmptyCatalog());
        var window = cards.Main;
        var summaries = new List<string>();
        window.SummaryChanged += summaries.Add;
        using var tray = options.Includes("Cards") ? new TrayIcon(cards) : null;
        try
        {
            var opening = Stopwatch.StartNew();
            window.Show();
            PumpUntil(() => window.Opacity == 1, TimeSpan.FromSeconds(10));
            var height = window.Height;
            var first = Columns(window)[0];
            // What matters is not waiting for the history: shown while still reading, or soon. A cold start
            // (WPF loading from disk right after a build) can pass 2 s without the card having waited.
            var wasReading = Find<TextBlock>(first, "Reading").Visibility == Visibility.Visible;
            Check(window.Opacity == 1 && (wasReading || opening.Elapsed < TimeSpan.FromSeconds(2)),
                $"the card opens without waiting for its data ({opening.Elapsed.TotalSeconds:0.0} s, {(wasReading ? "still reading" : "data in")})");
            PumpUntil(() => Find<StackPanel>(first, "Numbers").Visibility == Visibility.Visible, TimeSpan.FromSeconds(live ? 60 : 5));
            Check(Find<StackPanel>(first, "Numbers").Visibility == Visibility.Visible
                && Find<TextBlock>(first, "Reading").Visibility == Visibility.Collapsed, "the usage data is ready");
            Check(Math.Abs(window.Height - height) < 2, "loading preserves the card size");
            var handle = new WindowInteropHelper(window).Handle;

            RunSuite("Cards", () =>
            {
                CheckEdges(handle);
                RenderMenus(window, pictures);
                CheckTray(tray!, summaries, window);
                CheckViews(window);
                CheckRefresh(window, summaries);
                CheckPages(window, handle, providers.Count, pictures);
                CheckSplit(window, handle, pictures);
                CheckNewWindow(cards, pictures);
                CheckInstalls(cards, pictures);
                CheckDragKeepsProportions(window, handle, "one page");
            });
            RunSuite("Settings", () => CheckSettings(cards, preferences, pictures));
            RunSuite("Themes", () => WithSettings(settings => CheckThemes(cards, preferences, settings, pictures)));
            RunSuite("Fonts", () => WithSettings(settings => CheckFonts(cards, preferences, settings, pictures)));
            RunSuite("Mascot", () => WithSettings(settings => CheckPet(cards, preferences, settings, pictures)));
            RunSuite("Community", () => WithSettings(settings => CheckCommunity(cards, preferences, settings, pictures)));
            RunSuite("Updates", () => CheckUpdateBanner(cards, preferences, pictures));
            RunSuite("Providers", () =>
            {
                if (!options.Includes("Cards")) CheckPages(window, handle, providers.Count, pictures);
                CheckApps();
            });
        }
        finally { cards.Quit(); }

        void RunSuite(string name, Action run)
        {
            if (!options.Includes(name)) return;
            var before = _checks;
            var failed = _failures;
            var timer = Stopwatch.StartNew();
            run();
            Console.WriteLine($"UI {name}: {_checks - before} checks, {_failures - failed} failed ({timer.Elapsed.TotalSeconds:0.0} s)");
        }
        void WithSettings(Action<SettingsWindow> run)
        {
            Find<Button>(window, "SettingsButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            PumpUntil(() => cards.Settings is { IsVisible: true }, TimeSpan.FromSeconds(5));
            Check(cards.Settings is { IsVisible: true }, "the scenario's settings window opens");
            if (cards.Settings is not { } settings) return;
            try { run(settings); }
            finally { settings.Close(); Pump(TimeSpan.FromMilliseconds(100)); }
        }
    }
}
