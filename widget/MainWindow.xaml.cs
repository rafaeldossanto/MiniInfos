using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace MiniInfos;

public partial class MainWindow : Window
{
    private const string FiveHourKind = "five_hour";
    private const string WeekKind = "seven_day";
    private const double ScreenMargin = 16;
    private const double DesignWidth = 264;
    private const double MinScale = 0.75;
    private const double MaxScale = 3;
    private const int WmSizing = 0x0214;
    private const int WmExitSizeMove = 0x0232;

    private static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan FiveHours = TimeSpan.FromHours(5);
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);

    private static readonly Brush CalmMeter = Frozen(Color.FromRgb(0x4C, 0x8D, 0xF6));
    private static readonly Brush WarningMeter = Frozen(Color.FromRgb(0xE3, 0xA3, 0x3B));
    private static readonly Brush DangerMeter = Frozen(Color.FromRgb(0xE5, 0x53, 0x4B));

    private readonly UsageLedger _ledger = UsageLedger.Load();
    private readonly DispatcherTimer _timer = new() { Interval = RefreshEvery };
    private readonly RollingCount _fiveHourTokens;
    private readonly RollingCount _weekTokens;
    private readonly RollingCount _todayTokens;
    private readonly RollingCount _totalTokens;
    private double _designHeight;
    private Task? _refresh;
    private bool _isDetailed;
    private bool _placesAtCorner;
    private bool _isQuitting;

    /// <summary>What the tray icon reads when hovered: the card's own numbers, in three lines.</summary>
    public event Action<string>? SummaryChanged;

    public MainWindow()
    {
        InitializeComponent();

        _fiveHourTokens = new RollingCount(FiveHourDetail);
        _weekTokens = new RollingCount(WeekDetail);
        _todayTokens = new RollingCount(TodayTokens);
        _totalTokens = new RollingCount(TotalTokens);
        (CardSettings.Load()?.IsDetailed == true ? DetailedView : CompactView).IsChecked = true;

        // It keeps counting while hidden, so the tray icon's numbers stay current.
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        _ = RefreshAsync();
        IsVisibleChanged += async (_, _) =>
        {
            if (IsVisible && _designHeight > 0) await RefreshAsync();
        };
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(OnWindowMessage);
        Loaded += async (_, _) =>
        {
            // The card's own height, filled and at its design width, sets the proportions it keeps.
            await RefreshAsync();
            UpdateLayout();
            _designHeight = ActualHeight;
            SizeToContent = SizeToContent.Manual;
            MinWidth = DesignWidth * MinScale;
            MinHeight = _designHeight * MinScale;
            MaxWidth = DesignWidth * MaxScale;
            MaxHeight = _designHeight * MaxScale;

            var saved = CardSettings.Load();
            Resize(saved?.Scale ?? 1);
            Place(_placesAtCorner ? null : saved);
            Opacity = 1;
        };
        Closing += (_, e) =>
        {
            // Alt+F4 hides it like the ✕: it keeps waiting for Claude to open again.
            if (!_isQuitting)
            {
                e.Cancel = true;
                HideCard();
                return;
            }

            _timer.Stop();
            SaveSettings();
            _ledger.Flush();
        };
    }

    /// <summary>Shows the window again where it can be seen: what opening the app a second time does.</summary>
    public void BringBack()
    {
        ShowWithClaude();
        Activate();
    }

    /// <summary>Shows the window without taking the keyboard from the app that just opened.</summary>
    public void ShowWithClaude()
    {
        if (!IsVisible) Show();
        if (_designHeight > 0 && !IsOnScreen(Left, Top)) Place(saved: null);
    }

    public void HideCard()
    {
        SaveSettings();
        Hide();
    }

    public void BackToCorner()
    {
        if (_designHeight <= 0)
        {
            // Not shown yet: it is placed as it first shows.
            _placesAtCorner = true;
            ShowWithClaude();
            return;
        }

        ShowWithClaude();
        Place(saved: null);
        SaveSettings();
    }

    public void Quit()
    {
        _isQuitting = true;
        Application.Current.Shutdown();
    }

    /// <summary>Runs one refresh at a time: a call while one runs waits for that one.</summary>
    private Task RefreshAsync()
    {
        if (_refresh is { IsCompleted: false }) return _refresh;

        return _refresh = RefreshNowAsync();
    }

    private async Task RefreshNowAsync()
    {
        var now = DateTimeOffset.Now;
        var feed = FeedReader.Read(now);
        var fiveHour = feed.Limits.GetValueOrDefault(FiveHourKind);
        var week = feed.Limits.GetValueOrDefault(WeekKind);
        var todayStart = new DateTimeOffset(now.Date, now.Offset);
        var fiveHourStart = WindowStart(fiveHour, FiveHours, now);
        var weekStart = WindowStart(week, Week, now);

        var tokens = await Task.Run(() =>
        {
            _ledger.Scan();
            return _ledger.Summarize(todayStart, fiveHourStart, weekStart);
        });

        ShowLimit(fiveHour, tokens.FiveHour, "últimas 5h", now, FiveHourPercent, FiveHourBar, FiveHourDetail, _fiveHourTokens);
        ShowLimit(week, tokens.Week, "últimos 7 dias", now, WeekPercent, WeekBar, WeekDetail, _weekTokens);
        ShowTokens(tokens);
        ShowChat(feed.Latest);
        UpdatedText.Text = feed.LimitsUpdated is { } updated ? $"atualizado {Format.Ago(updated, now)}" : "sem dados ainda";
        SummaryChanged?.Invoke(
            $"MiniInfos\n5h {FiveHourPercent.Text} · Semanal {WeekPercent.Text}\nHoje {Format.Count(tokens.Today.Total)} tokens");
    }

    /// <summary>A count as the chosen view reads it: 1,4B, or every digit.</summary>
    private string CountText(long value) => _isDetailed ? Format.Full(value) : Format.Count(value);

    private static DateTimeOffset WindowStart(RateLimit? limit, TimeSpan length, DateTimeOffset now) =>
        limit?.ResetsAtTime is { } reset
            ? (reset > now ? reset - length : reset)
            : now - length;

    private void ShowLimit(
        RateLimit? limit,
        TokenCount tokens,
        string rollingLabel,
        DateTimeOffset now,
        TextBlock percent,
        ProgressBar bar,
        TextBlock detail,
        RollingCount used)
    {
        detail.ToolTip = Format.Breakdown(tokens, CountText);

        if (limit is null)
        {
            percent.Text = "—";
            bar.Value = 0;
            used.Show(tokens.Total, value => $"{CountText(value)} tokens nas {rollingLabel}");
            return;
        }

        var reset = limit.ResetsAtTime;
        var hasReset = reset is { } time && time <= now;
        percent.Text = hasReset ? "—" : Format.Percent(limit.PercentUsed);
        bar.Value = hasReset ? 0 : Math.Clamp(limit.PercentUsed, 0, 100);
        bar.Foreground = limit.PercentUsed switch
        {
            >= 90 => DangerMeter,
            >= 70 => WarningMeter,
            _ => CalmMeter,
        };
        var resetText = reset is { } at ? $" · {Format.Reset(at, now)}" : "";
        used.Show(tokens.Total, value => $"{CountText(value)} tokens{resetText}");
    }

    private void ShowTokens(TokenSummary tokens)
    {
        _todayTokens.Show(tokens.Today.Total, CountText);
        _totalTokens.Show(tokens.Total.Total, CountText);
        TodayBlock.ToolTip = Format.Breakdown(tokens.Today, CountText);
        TotalBlock.ToolTip = $"{Format.Breakdown(tokens.Total, CountText)}\nde todos os chats guardados em ~/.claude/projects";
    }

    private void ShowChat(FeedEntry? latest)
    {
        if (latest is null)
        {
            ChatText.Text = "Nenhum chat com o mod ainda";
            return;
        }

        var parts = new List<string> { ChatTitles.Of(latest) };
        if (latest.Context?.Percent is { } fill) parts.Add($"contexto {Format.Percent(fill)}");
        if (latest.CostUsd is { } cost) parts.Add(Format.Cost(cost));
        ChatText.Text = string.Join(" · ", parts);
        ChatText.ToolTip = $"{ChatText.Text}\nChat que respondeu por último";
    }

    private void Resize(double scale)
    {
        var clamped = Math.Clamp(scale, MinScale, MaxScale);
        Width = DesignWidth * clamped;
        Height = _designHeight * clamped;
    }

    private void Place(CardSettings? saved)
    {
        if (saved is not null && IsOnScreen(saved.Left, saved.Top))
        {
            Left = saved.Left;
            Top = saved.Top;
            return;
        }

        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - ScreenMargin;
        Top = area.Bottom - Height - ScreenMargin;
    }

    private bool IsOnScreen(double left, double top) =>
        left >= SystemParameters.VirtualScreenLeft
        && top >= SystemParameters.VirtualScreenTop
        && left + Width <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
        && top + Height <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

    private void SaveSettings()
    {
        if (_designHeight > 0) new CardSettings(Left, Top, Width / DesignWidth, _isDetailed).Save();
    }

    private IntPtr OnWindowMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Moving or resizing ended: keep where and how big it was left.
        if (message == WmExitSizeMove) SaveSettings();
        if (message != WmSizing || _designHeight <= 0) return IntPtr.Zero;

        var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var dragged = Marshal.PtrToStructure<PixelRect>(lParam);
        var fitted = AspectSizing.Fit(
            dragged,
            wParam.ToInt32(),
            DesignWidth / _designHeight,
            (int)Math.Round(DesignWidth * MinScale * dpi),
            (int)Math.Round(DesignWidth * MaxScale * dpi));
        Marshal.StructureToPtr(fitted, lParam, fDeleteOld: false);
        handled = true;

        return 1;
    }

    private void OnCardPressed(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void OnViewChanged(object sender, RoutedEventArgs e)
    {
        _isDetailed = DetailedView.IsChecked == true;
        SaveSettings();
        _ = RefreshAsync();
    }

    private void OnTopmostToggled(object sender, RoutedEventArgs e) => Topmost = TopmostItem.IsChecked;

    private void OnBackToCorner(object sender, RoutedEventArgs e) => BackToCorner();

    private void OnDefaultSize(object sender, RoutedEventArgs e)
    {
        Resize(1);
        if (!IsOnScreen(Left, Top)) Place(saved: null);
        SaveSettings();
    }

    private void OnHide(object sender, RoutedEventArgs e) => HideCard();

    private void OnQuit(object sender, RoutedEventArgs e) => Quit();

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
