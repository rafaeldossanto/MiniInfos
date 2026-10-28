using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using MiniInfos.Desktop.UI;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Mascot;
using MiniInfos.Providers;
using MiniInfos.Settings;
using System.ComponentModel;

namespace MiniInfos.Desktop;

public sealed class DesktopApp : Application
{
    private DesktopSession? _session;
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            if (SmokeCheck.Enabled && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MINIINFOS_DATA_DIR")))
                throw new InvalidOperationException("--smoke-ui requires MINIINFOS_DATA_DIR to isolate test settings.");
            _session = new DesktopSession(desktop, providers: SmokeCheck.Enabled ? SmokeCheck.Providers : null);
            _session.Start();
            desktop.ShutdownRequested += (_, _) => _session.Dispose();
            if (SmokeCheck.Enabled) SmokeCheck.Start(_session, desktop);
            desktop.Exit += (_, _) => _session.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed class DesktopSession : IDisposable
{
    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly SemaphoreSlim _reading = new(1);
    private readonly IReadOnlyList<IUsageProvider> _providers;
    private readonly InstalledProviders _installed;
    private readonly List<CardWindow> _cards = [];
    private readonly PetActivity _activity = new();
    private readonly CancellationTokenSource _cancellation = new();
    private TrayIcon? _tray;
    private SettingsWindow? _settings;
    private PetWindow? _pet;
    private bool _disposed;
    private bool? _wasInUse;
    private DateTimeOffset _flushed;
    private DateTimeOffset _installChecked;
    public Preferences Preferences { get; }
    public Palette Palette { get; private set; } = null!;
    public IReadOnlyList<IUsageProvider> Providers => _installed.Shown;
    public Dictionary<string, UsagePage> Pages { get; } = [];
    public event Action? Changed;
    public CancellationToken Cancellation => _cancellation.Token;
    public bool IsQuitting => _disposed;

    public DesktopSession(IClassicDesktopStyleApplicationLifetime lifetime, Preferences? preferences = null, IReadOnlyList<IUsageProvider>? providers = null)
    {
        _lifetime = lifetime; Preferences = preferences ?? Preferences.Load(); _providers = providers ?? UsageProviders.Create(); ApplyPalette();
        _installed = new InstalledProviders(_providers);
    }
    public void Start()
    {
        L.Follow(Preferences); ApplyPalette();
        Preferences.PropertyChanged += PreferenceChanged;
        L.Current.Changed += LanguageChanged;
        Application.Current!.PlatformSettings!.ColorValuesChanged += (_, _) => { if (Preferences.Theme == ThemeChoice.System) { ApplyPalette(); Changed?.Invoke(); } };
        var main = OpenCard(CardStore.Main);
        _lifetime.MainWindow = main;
        foreach (var store in CardStore.Extras()) OpenCard(store);
        MakeTray(); FollowPet();
        if (Application.Current!.TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activation)
            activation.Activated += (_, e) => { if (e.Kind == ActivationKind.Reopen) ShowCards(); };
        if (Program.Instance is { } instance) _ = instance.ListenAsync(() => Dispatcher.UIThread.Post(ShowCards));
        _timer.Tick += Tick; _timer.Start(); _ = RefreshAsync();
    }
    private void ApplyPalette()
    {
        Palette = new Palette(Preferences);
        Application.Current!.RequestedThemeVariant = Palette.Light ? Avalonia.Styling.ThemeVariant.Light : Avalonia.Styling.ThemeVariant.Dark;
        if (Palette["Accent"] is Avalonia.Media.ISolidColorBrush accent && Application.Current.Styles.OfType<FluentTheme>().FirstOrDefault() is { } theme)
        {
            foreach (var variant in new[] { Avalonia.Styling.ThemeVariant.Dark, Avalonia.Styling.ThemeVariant.Light })
                theme.Palettes[variant] = new ColorPaletteResources { Accent = accent.Color };
        }
    }
    private void PreferenceChanged(object? sender, PropertyChangedEventArgs e)
    {
        ApplyPalette(); FollowPet(); Changed?.Invoke();
    }
    private void LanguageChanged(object? sender, EventArgs e) { MakeTray(); Changed?.Invoke(); _ = RefreshAsync(); }
    private async void Tick(object? sender, EventArgs e) => await RefreshAsync();

    public async Task RefreshAsync(bool force = false)
    {
        if (_disposed || !await _reading.WaitAsync(0)) return;
        try
        {
            if (force || DateTimeOffset.Now - _installChecked > TimeSpan.FromMinutes(1))
            { await _installed.CheckAsync(); _installChecked = DateTimeOffset.Now; }
            foreach (var provider in _providers)
            {
                try { Pages[provider.Id] = await provider.ReadAsync(DateTimeOffset.Now, _cards.Any(card => card.Shows(provider.Id)), force); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception)
                { System.Diagnostics.Trace.TraceError("{0}: {1}", provider.Id, e); }
            }
            if (_disposed) return;
            _activity.Observe(Pages.Select(pair => PetUsage.Of(pair.Key, pair.Value, DateTimeOffset.Now)).ToArray(), DateTimeOffset.Now);
            Changed?.Invoke();
            if (DateTimeOffset.Now - _flushed > TimeSpan.FromMinutes(1)) { foreach (var provider in _providers) provider.Flush(); _flushed = DateTimeOffset.Now; }
            var inUse = await Task.Run(() => _providers.Any(provider => provider.IsInUse()));
            if (_disposed) return;
            if (Preferences.FollowsApps && inUse != _wasInUse) { if (inUse) ShowCards(); else if (_wasInUse is not null && _tray is not null) HideCards(); }
            _wasInUse = inUse;
        }
        finally
        {
            if (_disposed) foreach (var provider in _providers) provider.Flush();
            _reading.Release();
        }
    }
    public CardWindow OpenCard(CardStore store)
    {
        var window = new CardWindow(this, store); _cards.Add(window);
        window.Closed += (_, _) => { _cards.Remove(window); if (!_disposed) store.Forget(); };
        window.Show(); return window;
    }
    public void NewCard(string id) => OpenCard(CardStore.NewExtra(new CardSettings(100, 100, Page: id)));
    public void ShowCards() { foreach (var card in _cards) { card.Show(); card.Activate(); } }
    public void HideCards() { foreach (var card in _cards) card.Hide(); }
    public void OpenSettings()
    {
        if (_settings is null) { _settings = new SettingsWindow(this); _settings.Closed += (_, _) => _settings = null; }
        _settings.Show(); _settings.Activate();
    }
    private void FollowPet()
    {
        if (!Preferences.PetEnabled) { _pet?.Close(); _pet = null; return; }
        if (_pet is not null && _pet.AppearanceId == Preferences.PetId) { _pet.SetSize(); return; }
        _pet?.Close(); _pet = new PetWindow(this, _activity); _pet.Show();
    }
    private void MakeTray()
    {
        _tray?.Dispose();
        using var icon = Avalonia.Platform.AssetLoader.Open(new Uri("avares://MiniInfos.Desktop/Assets/MiniInfos.ico"));
        _tray = new TrayIcon { Icon = new WindowIcon(icon), ToolTipText = "MiniInfos" };
        var menu = new NativeMenu();
        foreach (var (label, action) in new (string, Action)[] { ("Mostrar MiniInfos", ShowCards), ("Configurações", OpenSettings), ("Atualizar agora", () => _ = RefreshAsync(true)), ("Sair do MiniInfos", Quit) })
        { var item = new NativeMenuItem(L.Text(label)); item.Click += (_, _) => action(); menu.Items.Add(item); }
        _tray.Menu = menu; _tray.Clicked += (_, _) => ShowCards(); _tray.IsVisible = true;
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { _tray });
    }
    public void Quit() { Dispose(); _lifetime.Shutdown(); }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _timer.Stop(); _cancellation.Cancel();
        Preferences.PropertyChanged -= PreferenceChanged; L.Current.Changed -= LanguageChanged;
        foreach (var card in _cards) card.Save();
        _tray?.Dispose(); _pet?.Close();
        // An in-flight read flushes in its own finally; do not race a ledger scan at shutdown.
        if (_reading.Wait(0)) { try { foreach (var provider in _providers) provider.Flush(); } finally { _reading.Release(); } }
    }
}
