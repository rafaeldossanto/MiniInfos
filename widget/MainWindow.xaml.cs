using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MiniInfos;

/// <summary>
/// The card: one page per usage provider, drawn the same way from each provider's
/// <see cref="UsagePage"/>. It knows no provider by name.
/// </summary>
public partial class MainWindow : Window
{
    private const double ScreenMargin = 16;
    private const double DesignWidth = 264;
    private const double MinScale = 0.75;
    private const double MaxScale = 3;
    private const int WmSizing = 0x0214;
    private const int WmExitSizeMove = 0x0232;
    private const int WmMouseHorizontalWheel = 0x020E;
    private const int WheelNotch = 120;

    private static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SpinTurn = TimeSpan.FromMilliseconds(800);
    private static readonly TimeSpan MinSpin = TimeSpan.FromMilliseconds(600);

    /// <summary>A touchpad swipe sends a burst of sideways wheel steps: after a page turns, the rest of the burst waits this long.</summary>
    private static readonly TimeSpan SwipeSettles = TimeSpan.FromMilliseconds(450);

    private static readonly Brush CalmMeter = Frozen(Color.FromRgb(0x4C, 0x8D, 0xF6));
    private static readonly Brush WarningMeter = Frozen(Color.FromRgb(0xE3, 0xA3, 0x3B));
    private static readonly Brush DangerMeter = Frozen(Color.FromRgb(0xE5, 0x53, 0x4B));
    private static readonly Brush OtherPageDot = Frozen(Color.FromRgb(0x55, 0x56, 0x5A));

    private readonly IReadOnlyList<IUsageProvider> _providers;
    private readonly DispatcherTimer _timer = new() { Interval = RefreshEvery };
    private readonly RollingCount _shortTokens;
    private readonly RollingCount _longTokens;
    private readonly RollingCount _todayTokens;
    private readonly RollingCount _totalTokens;
    private double _designHeight;
    private Task? _refresh;
    private int _page;
    private int _swipe;
    private long _swipeSettlesAt;
    private bool _isDetailed;
    private bool _placesAtCorner;
    private bool _isQuitting;

    /// <summary>What the tray icon reads when hovered: the page's own numbers, in three lines.</summary>
    public event Action<string>? SummaryChanged;

    /// <param name="providers">One page each, in this order.</param>
    public MainWindow(IReadOnlyList<IUsageProvider> providers)
    {
        if (providers.Count == 0) throw new ArgumentException("The card needs a provider to show.", nameof(providers));

        _providers = providers;
        InitializeComponent();

        _shortTokens = new RollingCount(ShortDetail);
        _longTokens = new RollingCount(LongDetail);
        _todayTokens = new RollingCount(TodayTokens);
        _totalTokens = new RollingCount(TotalTokens);

        var settings = CardSettings.Load();
        _page = Math.Max(0, IndexOf(settings?.Page));
        BuildPageDots();
        (settings?.IsDetailed == true ? DetailedView : CompactView).IsChecked = true;

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
            // Alt+F4 hides it like the ✕: it keeps waiting for an app it follows to open again.
            if (!_isQuitting)
            {
                e.Cancel = true;
                HideCard();
                return;
            }

            _timer.Stop();
            SaveSettings();
            foreach (var provider in _providers) provider.Flush();
        };
    }

    /// <summary>Shows the window again where it can be seen: what opening the app a second time does.</summary>
    public void BringBack()
    {
        ShowWithAssistant();
        Activate();
    }

    /// <summary>Shows the window without taking the keyboard from the app that just opened.</summary>
    public void ShowWithAssistant()
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
            ShowWithAssistant();
            return;
        }

        ShowWithAssistant();
        Place(saved: null);
        SaveSettings();
    }

    public void Quit()
    {
        _isQuitting = true;
        Application.Current.Shutdown();
    }

    /// <summary>
    /// A refresh the person asked for: it waits for one already running, then reads everything
    /// again, the refresh icon turning meanwhile (for a moment at least, so the press shows).
    /// </summary>
    public async Task ForceRefreshAsync()
    {
        if (!RefreshButton.IsEnabled) return;

        RefreshButton.IsEnabled = false;
        RefreshSpin.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, SpinTurn) { RepeatBehavior = RepeatBehavior.Forever });
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            if (_refresh is { IsCompleted: false } running) await running;
            await RefreshAsync(force: true);

            var left = MinSpin - Stopwatch.GetElapsedTime(startedAt);
            if (left > TimeSpan.Zero) await Task.Delay(left);
        }
        finally
        {
            RefreshSpin.BeginAnimation(RotateTransform.AngleProperty, null);
            RefreshButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Runs one refresh at a time: a call while one runs waits for that one. It always starts from the
    /// window's dispatcher, so its awaits come back to this thread whoever asks: the constructor
    /// (choosing the saved view), the tray icon's WinForms events or a host with no message loop yet.
    /// </summary>
    private Task RefreshAsync(bool force = false)
    {
        if (_refresh is { IsCompleted: false }) return _refresh;

        return _refresh = Dispatcher.InvokeAsync(() => RefreshNowAsync(force)).Task.Unwrap();
    }

    private async Task RefreshNowAsync(bool force)
    {
        var now = DateTimeOffset.Now;
        var shown = _page;

        // Every provider reads, so each ledger keeps up with its logs; the page on screen alone may
        // pay for costlier reads, and only it is forced.
        var pages = await Task.WhenAll(_providers.Select((provider, index) =>
            provider.ReadAsync(now, isShown: index == shown, force: force && index == shown)));

        Show(pages[_page], now);
    }

    private void Show(UsagePage page, DateTimeOffset now)
    {
        PageTitle.Text = page.Title;
        ShowWindow(page.ShortWindow, page.TokensOrigin, now, ShortLabel, ShortPercent, ShortBar, ShortDetail, _shortTokens);
        ShowWindow(page.LongWindow, page.TokensOrigin, now, LongLabel, LongPercent, LongBar, LongDetail, _longTokens);

        _todayTokens.Show(page.Today.Total, CountText);
        _totalTokens.Show(page.Total.Total, CountText);
        TodayBlock.ToolTip = $"{Format.Breakdown(page.Today, CountText)}\n{page.TokensOrigin}";
        TotalBlock.ToolTip = $"{Format.Breakdown(page.Total, CountText)}\n{page.TokensOrigin}";

        ChatText.Text = page.Activity;
        ChatText.ToolTip = $"{page.Activity}\n{page.ActivityHint}";
        UpdatedText.Text = page.Updated;
        RefreshButton.ToolTip = page.RefreshHint;

        SummaryChanged?.Invoke(
            $"MiniInfos · {page.Title}\n{ShortLabel.Text} {ShortPercent.Text} · {LongLabel.Text} {LongPercent.Text}\n" +
            $"Hoje {Format.Count(page.Today.Total)} tokens");
    }

    /// <summary>A count as the chosen view reads it: 1,4B, or every digit.</summary>
    private string CountText(long value) => _isDetailed ? Format.Full(value) : Format.Count(value);

    private void ShowWindow(
        WindowLine line,
        string tokensOrigin,
        DateTimeOffset now,
        TextBlock label,
        TextBlock percent,
        ProgressBar bar,
        TextBlock detail,
        RollingCount used)
    {
        var length = line.Window.Length;
        label.Text = Format.WindowLabel(length);
        detail.ToolTip = $"{Format.Breakdown(line.Tokens, CountText)}\n{tokensOrigin}";

        if (line.Window.Limit is not { } limit)
        {
            percent.Text = "—";
            bar.Value = 0;
            used.Show(line.Tokens.Total, value => $"{CountText(value)} tokens nas {Format.WindowSpan(length)}");
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
        used.Show(line.Tokens.Total, value => $"{CountText(value)} tokens{resetText}");
    }

    private int IndexOf(string? providerId)
    {
        for (var index = 0; index < _providers.Count; index++)
        {
            if (_providers[index].Id == providerId) return index;
        }

        return -1;
    }

    /// <summary>Turns to a page, wrapping around both ends; the counts start over, not rolling from another provider's.</summary>
    private void ShowPage(int page)
    {
        var next = (page % _providers.Count + _providers.Count) % _providers.Count;
        if (next == _page) return;

        _page = next;
        foreach (var count in (RollingCount[])[_shortTokens, _longTokens, _todayTokens, _totalTokens]) count.Reset();
        UpdatePageDots();
        SaveSettings();
        _ = RefreshAsync();
    }

    private void BuildPageDots()
    {
        Pager.Visibility = _providers.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        PageDots.Children.Clear();
        for (var index = 0; index < _providers.Count; index++)
        {
            var page = index;
            var dot = new Ellipse { Width = 4, Height = 4, Margin = new Thickness(2, 0, 2, 0), Cursor = Cursors.Hand };
            dot.MouseLeftButtonDown += (_, e) =>
            {
                ShowPage(page);
                e.Handled = true;
            };
            PageDots.Children.Add(dot);
        }

        UpdatePageDots();
    }

    private void UpdatePageDots()
    {
        for (var index = 0; index < PageDots.Children.Count; index++)
        {
            ((Ellipse)PageDots.Children[index]).Fill = index == _page ? CalmMeter : OtherPageDot;
        }
    }

    /// <summary>A sideways wheel step (a touchpad's two-finger swipe, a tilted wheel) turns the page once per swipe.</summary>
    private void Swipe(int delta)
    {
        if (Stopwatch.GetTimestamp() < _swipeSettlesAt)
        {
            _swipe = 0;
            return;
        }

        _swipe += delta;
        if (Math.Abs(_swipe) < WheelNotch) return;

        ShowPage(_page + Math.Sign(_swipe));
        _swipe = 0;
        _swipeSettlesAt = Stopwatch.GetTimestamp() + (long)(SwipeSettles.TotalSeconds * Stopwatch.Frequency);
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
        if (_designHeight > 0) new CardSettings(Left, Top, Width / DesignWidth, _isDetailed, _providers[_page].Id).Save();
    }

    private IntPtr OnWindowMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (message)
        {
            case WmExitSizeMove:
                // Moving or resizing ended: keep where and how big it was left.
                SaveSettings();
                return IntPtr.Zero;

            case WmMouseHorizontalWheel:
                // The step rides in the high word of wParam; positive leans right, to the next page.
                Swipe(unchecked((short)((long)wParam >> 16)));
                handled = true;
                return IntPtr.Zero;

            case WmSizing when _designHeight > 0:
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

            default:
                return IntPtr.Zero;
        }
    }

    /// <summary>Dragging anywhere on the card moves it, in any direction.</summary>
    private void OnCardPressed(object sender, MouseButtonEventArgs e)
    {
        Card.Focus();
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    /// <summary>Shift with the wheel turns the page too, for a mouse without a sideways wheel.</summary>
    private void OnCardWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Shift) return;

        ShowPage(_page + (e.Delta < 0 ? 1 : -1));
        e.Handled = true;
    }

    private void OnPageKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Left or Key.Right)) return;

        ShowPage(_page + (e.Key == Key.Right ? 1 : -1));
        e.Handled = true;
    }

    private void OnPreviousPage(object sender, RoutedEventArgs e) => ShowPage(_page - 1);

    private void OnNextPage(object sender, RoutedEventArgs e) => ShowPage(_page + 1);

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

    private async void OnRefresh(object sender, RoutedEventArgs e) => await ForceRefreshAsync();

    private void OnHide(object sender, RoutedEventArgs e) => HideCard();

    private void OnQuit(object sender, RoutedEventArgs e) => Quit();

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
