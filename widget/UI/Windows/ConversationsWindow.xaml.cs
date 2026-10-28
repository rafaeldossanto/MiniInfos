using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Providers;
using MiniInfos.Settings;
using MiniInfos.UI.Controls;
using MiniInfos.UI.Formatting;
using MiniInfos.UI.Themes;
using Path = System.Windows.Shapes.Path;

namespace MiniInfos.UI.Windows;

/// <summary>What the conversations card keeps between runs: where it stood, its view and its filter.</summary>
public sealed record ConversationsSettings(double? Left, double? Top, bool IsDetailed, string? Provider, double Scale = 1);

/// <summary>
/// The conversations card: each chat or session of the installed providers with the tokens it spent, the one that
/// answered last on top. It shows what the providers' ledgers already hold, which the usage card keeps current even
/// while hidden, so it asks every few seconds without reading a log of its own.
/// </summary>
public partial class ConversationsWindow : Window, ISettingsCard
{
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(3);

    private const double DesignWidth = 296;

    /// <summary>Between this card and the one it opens beside.</summary>
    private const double Gap = 12;

    private readonly Func<IReadOnlyList<IUsageProvider>> _providers;
    private readonly JsonFile<ConversationsSettings> _store;
    private readonly DispatcherTimer _timer = new() { Interval = RefreshEvery };
    private readonly List<(string Id, Path Mark)> _filters = [];
    private readonly Dictionary<(string Provider, string Key), ConversationView> _rows = [];
    private double _designHeight;
    private bool _fitPending;
    private bool _refreshing;
    private Window? _beside;
    private string? _filter;
    private bool _isDetailed;
    private bool _isPlaced;
    private bool _isQuitting;

    /// <param name="providers">The installed providers, asked on each refresh: apps come and go.</param>
    public ConversationsWindow(Func<IReadOnlyList<IUsageProvider>> providers, Preferences preferences, string? storePath = null)
    {
        _providers = providers;
        _store = new JsonFile<ConversationsSettings>(storePath ?? System.IO.Path.Combine(AppPaths.Data, "conversations.json"));
        InitializeComponent();
        _ = new CardResize(this, () => new Size(DesignWidth, _designHeight), Save);

        var saved = _store.Load();
        _filter = saved?.Provider;
        _isDetailed = saved?.IsDetailed == true;
        (_isDetailed ? DetailedView : CompactView).IsChecked = true;
        SetBinding(TopmostProperty, new Binding(nameof(Preferences.IsTopmost)) { Source = preferences });
        TopmostItem.SetBinding(MenuItem.IsCheckedProperty, new Binding(nameof(Preferences.IsTopmost)) { Source = preferences, Mode = BindingMode.TwoWay });
        ShowProviders();

        Loaded += (_, _) =>
        {
            SizeToContent = SizeToContent.Manual;
            Fit(saved?.Scale ?? 1);
            Place(_beside);
        };

        _timer.Tick += (_, _) => ShowConversations();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
            {
                _timer.Stop();
                return;
            }

            ShowConversations();
            _timer.Start();
        };
        ThemeManager.Changed += ShowConversations;
        AppFonts.Changed += ShowConversations;
        L.Current.Changed += OnLanguageChanged;
        Closing += (_, e) =>
        {
            if (_isQuitting)
            {
                _timer.Stop();
                Save();
                return;
            }

            // Alt+F4 hides it like the ✕: it comes back from its button or as the main card.
            e.Cancel = true;
            HideCard();
        };
        Closed += (_, _) =>
        {
            ThemeManager.Changed -= ShowConversations;
            AppFonts.Changed -= ShowConversations;
            L.Current.Changed -= OnLanguageChanged;
        };
    }

    /// <summary>The settings window, from the gear.</summary>
    public event Action? SettingsRequested;
    public event Action? ViewChanged;
    public event Action? QuitRequested;

    Window ISettingsCard.Window => this;

    public bool IsDetailed
    {
        get => _isDetailed;
        set => (value ? DetailedView : CompactView).IsChecked = true;
    }

    /// <summary>The usage card, from the button beside the gear.</summary>
    public event Action? UsageRequested;

    /// <summary>↻: the providers read their logs again now.</summary>
    public event Func<Task>? RefreshRequested;

    /// <summary>The conversations listed now, in their order.</summary>
    public IReadOnlyList<Conversation> Shown { get; private set; } = [];

    /// <summary>The provider the list is filtered to, or null for every one.</summary>
    public string? Filter => _filter;

    /// <summary>Shows the card without taking the keyboard: the first time beside <paramref name="beside"/>, else where it was left.</summary>
    public void ShowWithAssistant(Window? beside)
    {
        _beside = beside;
        if (!IsVisible) Show();
        if (IsLoaded && !_isPlaced) Place(beside);
        else if (IsLoaded) KeepOnScreen();
    }

    public void BringBack() => BringBack(null);

    /// <summary>Shows the card in front, what opening it from its button does.</summary>
    public void BringBack(Window? beside)
    {
        ShowWithAssistant(beside);
        Activate();
    }

    public void HideCard()
    {
        Save();
        Hide();
    }

    /// <summary>Lets the app's shutdown close this card as it is, saved, instead of hiding it.</summary>
    public void PrepareToQuit() => _isQuitting = true;

    /// <summary>One mark per installed provider to filter by; a filter whose app went away shows them all again.</summary>
    public void ShowProviders()
    {
        Filters.Children.Clear();
        _filters.Clear();
        var providers = _providers();
        if (_filter is not null && providers.All(provider => provider.Id != _filter)) _filter = null;

        foreach (var provider in providers)
        {
            var id = provider.Id;
            var mark = new Path
            {
                Data = TryFindResource($"ProviderIcon.{id}") as Geometry ?? (Geometry)FindResource("ProviderIcon.default"),
                Width = 12,
                Height = 12,
                Stretch = Stretch.Uniform,
            };
            var button = new Button
            {
                Style = (Style)FindResource("PageArrow"),
                Width = 20,
                Height = 20,
                Content = mark,
                ToolTip = L.Text("Só esta IA; de novo, todas"),
            };
            button.Click += (_, _) => FilterBy(id);
            Filters.Children.Add(button);
            _filters.Add((id, mark));
        }

        ShowConversations();
    }

    /// <summary>Shows only this provider's conversations; asked again for the same one, every provider's.</summary>
    public void FilterBy(string? providerId)
    {
        _filter = _filter == providerId ? null : providerId;
        ShowConversations();
        Save();
    }

    public void ShowConversations()
    {
        Header.Title = L.Text("Conversas");
        Header.RefreshToolTip = L.Text("Atualizar agora");
        Header.DismissToolTip = L.Text("Esconder");
        var now = DateTimeOffset.Now;
        var conversations = ConversationList.Recent(_providers(), _filter);
        Shown = conversations;
        var current = conversations.Select(conversation => (conversation.ProviderId, conversation.Key)).ToHashSet();
        foreach (var key in _rows.Keys.Where(key => !current.Contains(key)).ToArray())
        {
            List.Items.Remove(_rows[key]);
            _rows.Remove(key);
        }
        for (var index = 0; index < conversations.Count; index++)
        {
            var conversation = conversations[index];
            var key = (conversation.ProviderId, conversation.Key);
            if (!_rows.TryGetValue(key, out var row)) _rows[key] = row = new ConversationView();
            if (List.Items.IndexOf(row) != index)
            {
                List.Items.Remove(row);
                List.Items.Insert(index, row);
            }
            row.Show(conversation, now, Count, index == 0, index == conversations.Count - 1);
        }
        Empty.Visibility = conversations.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var total = conversations.Aggregate(0L, (sum, conversation) => sum + conversation.Tokens.Total);
        NumericText.Show(Summary, conversations.Count == 1
            ? L.Format("1 conversa · {0} tokens", Count(total))
            : L.Format("{0} conversas · {1} tokens", conversations.Count, Count(total)));

        // Every mark lit when nothing filters; filtered, only the chosen provider's.
        foreach (var (id, mark) in _filters) mark.SetResourceReference(Path.FillProperty, _filter is null || _filter == id ? "PrimaryText" : "IconOther");
        ScheduleFit();
    }

    private void ScheduleFit()
    {
        if (!IsLoaded || _fitPending || _isQuitting) return;
        _fitPending = true;
        Dispatcher.InvokeAsync(() =>
        {
            _fitPending = false;
            if (!_isQuitting) Fit(Width / DesignWidth);
        }, DispatcherPriority.Loaded);
    }

    private void Fit(double scale)
    {
        Card.InvalidateMeasure();
        Card.Measure(new Size(DesignWidth, double.PositiveInfinity));
        _designHeight = Card.DesiredSize.Height;
        MinWidth = DesignWidth * CardResize.MinScale;
        MaxWidth = DesignWidth * CardResize.MaxScale;
        MinHeight = _designHeight * CardResize.MinScale;
        MaxHeight = _designHeight * CardResize.MaxScale;
        var keep = double.IsFinite(scale) ? Math.Clamp(scale, CardResize.MinScale, CardResize.MaxScale) : 1;
        Width = DesignWidth * keep;
        Height = _designHeight * keep;
        if (_isPlaced) KeepOnScreen();
    }

    public void ResetSize()
    {
        Fit(1);
        KeepOnScreen();
        Save();
    }

    public void BackToCorner()
    {
        ShowWithAssistant(null);
        PlaceAtCorner();
        _isPlaced = true;
        Save();
    }

    private string Count(long value) => _isDetailed ? Format.Full(value) : Format.Count(value);

    /// <summary>Where it was left, if that is still on a screen; else beside the card it opens from; else in the corner.</summary>
    private void Place(Window? beside)
    {
        var saved = _store.Load();
        if (saved is { Left: { } left, Top: { } top } && IsOnScreen(left, top))
        {
            Left = left;
            Top = top;
        }
        else if (beside is { IsVisible: true })
        {
            Left = beside.Left - ActualWidth - Gap >= SystemParameters.VirtualScreenLeft ? beside.Left - ActualWidth - Gap : beside.Left + beside.ActualWidth + Gap;
            Top = beside.Top;
        }
        else
        {
            PlaceAtCorner();
        }

        _isPlaced = true;
        KeepOnScreen();
    }

    private void PlaceAtCorner()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 16;
        Top = area.Bottom - Height - 16;
    }

    private void KeepOnScreen()
    {
        Left = Math.Clamp(Left, SystemParameters.VirtualScreenLeft,
            Math.Max(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width));
        Top = Math.Clamp(Top, SystemParameters.VirtualScreenTop,
            Math.Max(SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height));
    }

    private static bool IsOnScreen(double left, double top) =>
        left >= SystemParameters.VirtualScreenLeft
        && top >= SystemParameters.VirtualScreenTop
        && left + 40 <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
        && top + 40 <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

    private void Save()
    {
        if (_isPlaced) _store.Save(new ConversationsSettings(Left, Top, _isDetailed, _filter, Width / DesignWidth));
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => ShowProviders();

    /// <summary>Dragged anywhere but its buttons, which take their own presses.</summary>
    private void OnCardPressed(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private async void OnRefresh(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        _refreshing = true;
        Header.RefreshButton.IsEnabled = false;
        Header.SetRefreshing(true);
        try
        {
            if (RefreshRequested is { } refresh) await refresh();
            ShowConversations();
        }
        finally
        {
            Header.SetRefreshing(false);
            Header.RefreshButton.IsEnabled = true;
            _refreshing = false;
        }
    }

    private void OnDismiss(object sender, RoutedEventArgs e) => HideCard();

    private void OnSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();

    private void OnUsage(object sender, RoutedEventArgs e) => UsageRequested?.Invoke();

    private void OnBackToCorner(object sender, RoutedEventArgs e) => BackToCorner();
    private void OnDefaultSize(object sender, RoutedEventArgs e) => ResetSize();
    private void OnQuit(object sender, RoutedEventArgs e) => QuitRequested?.Invoke();

    private void OnViewChanged(object sender, RoutedEventArgs e)
    {
        _isDetailed = DetailedView.IsChecked == true;
        if (!IsInitialized) return;
        ShowConversations();
        Save();
        ViewChanged?.Invoke();
    }
}
