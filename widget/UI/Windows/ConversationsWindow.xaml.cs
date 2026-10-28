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
using MiniInfos.UI.Formatting;
using MiniInfos.UI.Themes;
using Path = System.Windows.Shapes.Path;

namespace MiniInfos.UI.Windows;

/// <summary>What the conversations card keeps between runs: where it stood, its view and its filter.</summary>
public sealed record ConversationsSettings(double? Left, double? Top, bool IsDetailed, string? Provider);

/// <summary>One line of the list, ready to bind.</summary>
public sealed record ConversationRow(
    Geometry Icon,
    Brush IconBrush,
    string Title,
    string Where,
    string Tokens,
    string Breakdown,
    string Context,
    Visibility LiveVisibility,
    Visibility DividerVisibility);

/// <summary>
/// The conversations card: each chat or session of the installed providers with the tokens it spent, the one that
/// answered last on top. It shows what the providers' ledgers already hold, which the usage card keeps current even
/// while hidden, so it asks every few seconds without reading a log of its own.
/// </summary>
public partial class ConversationsWindow : Window
{
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(3);

    /// <summary>Answered this recently, a conversation reads as live: the dot beside "agora".</summary>
    private static readonly TimeSpan LiveFor = TimeSpan.FromMinutes(2);

    /// <summary>Between this card and the one it opens beside.</summary>
    private const double Gap = 12;

    private readonly Func<IReadOnlyList<IUsageProvider>> _providers;
    private readonly JsonFile<ConversationsSettings> _store;
    private readonly DispatcherTimer _timer = new() { Interval = RefreshEvery };
    private readonly List<(string Id, Path Mark)> _filters = [];
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

        var saved = _store.Load();
        _filter = saved?.Provider;
        _isDetailed = saved?.IsDetailed == true;
        (_isDetailed ? DetailedView : CompactView).IsChecked = true;
        SetBinding(TopmostProperty, new Binding(nameof(Preferences.IsTopmost)) { Source = preferences });
        ShowProviders();

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
            L.Current.Changed -= OnLanguageChanged;
        };
    }

    /// <summary>The settings window, from the gear.</summary>
    public event Action? SettingsRequested;

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
        if (!IsVisible) Show();
        if (!_isPlaced) Place(beside);
    }

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
        var now = DateTimeOffset.Now;
        var conversations = ConversationList.Recent(_providers(), _filter);
        Shown = conversations;
        List.ItemsSource = conversations.Select((conversation, index) => Row(conversation, index, conversations.Count, now)).ToArray();
        Empty.Visibility = conversations.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var total = conversations.Aggregate(0L, (sum, conversation) => sum + conversation.Tokens.Total);
        Summary.Text = conversations.Count == 1
            ? L.Format("1 conversa · {0} tokens", Count(total))
            : L.Format("{0} conversas · {1} tokens", conversations.Count, Count(total));

        // Every mark lit when nothing filters; filtered, only the chosen provider's.
        foreach (var (id, mark) in _filters) mark.SetResourceReference(Path.FillProperty, _filter is null || _filter == id ? "PrimaryText" : "IconOther");
    }

    private ConversationRow Row(Conversation conversation, int index, int count, DateTimeOffset now)
    {
        var at = DateTimeOffset.FromUnixTimeSeconds(conversation.LastAt);
        var ago = Format.Ago(at, now);
        return new ConversationRow(
            TryFindResource($"ProviderIcon.{conversation.ProviderId}") as Geometry ?? (Geometry)FindResource("ProviderIcon.default"),
            (Brush)FindResource(index == 0 ? "PrimaryText" : "DimText"),
            conversation.Title,
            conversation.Project is { } project ? $"{project} · {ago}" : ago,
            Count(conversation.Tokens.Total),
            Format.Breakdown(conversation.Tokens, Count),
            conversation.ContextPercent is { } fill ? L.Format("contexto {0}", Format.Percent(fill)) : "",
            now - at < LiveFor ? Visibility.Visible : Visibility.Collapsed,
            index < count - 1 ? Visibility.Visible : Visibility.Collapsed);
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
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 16;
            Top = area.Bottom - ActualHeight - 16;
        }

        _isPlaced = true;
    }

    private static bool IsOnScreen(double left, double top) =>
        left >= SystemParameters.VirtualScreenLeft
        && top >= SystemParameters.VirtualScreenTop
        && left + 40 <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
        && top + 40 <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

    private void Save()
    {
        if (_isPlaced) _store.Save(new ConversationsSettings(Left, Top, _isDetailed, _filter));
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => ShowProviders();

    /// <summary>Dragged anywhere but its buttons, which take their own presses.</summary>
    private void OnCardPressed(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private async void OnRefresh(object sender, RoutedEventArgs e)
    {
        if (RefreshRequested is { } refresh) await refresh();
        ShowConversations();
    }

    private void OnDismiss(object sender, RoutedEventArgs e) => HideCard();

    private void OnSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();

    private void OnUsage(object sender, RoutedEventArgs e) => UsageRequested?.Invoke();

    private void OnViewChanged(object sender, RoutedEventArgs e)
    {
        _isDetailed = DetailedView.IsChecked == true;
        if (IsInitialized) ShowConversations();
    }
}
