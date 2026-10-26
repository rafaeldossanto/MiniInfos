using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MiniInfos;

/// <summary>Which card a window is: the main one, or one opened with "Nova janela".</summary>
public enum CardKind
{
    /// <summary>Follows Claude and Codex, holds the tray's numbers; ✕ hides it.</summary>
    Main,

    /// <summary>Opened beside the main one; ✕ closes it for good.</summary>
    Extra,
}

/// <summary>
/// A card: the pages of the usage providers, one or two side by side (<see cref="PageSelection"/>),
/// each drawn by a <see cref="PageView"/> from its provider's <see cref="UsagePage"/>. It knows no
/// provider by name.
/// </summary>
public partial class MainWindow : Window
{
    private const double ScreenMargin = 16;
    private const double CascadeOffset = 28;
    private const double MinScale = 0.75;
    private const double MaxScale = 3;
    private const int WmSizing = 0x0214;
    private const int WmExitSizeMove = 0x0232;
    private const int WmMouseHorizontalWheel = 0x020E;
    private const int WheelNotch = 120;

    /// <summary>
    /// The card's width at scale 1 with one page; each page beside it adds a column and its gap. A
    /// column is what one page leaves inside the card: 296, less the border (1+1) and padding (16+16).
    /// </summary>
    private const double SinglePageWidth = 296;
    private const double ColumnWidth = 262;
    private const double ColumnGap = 25;

    /// <summary>The card's corner radius (16) less its border, for what is drawn inside it.</summary>
    private const double ArtCornerRadius = 15;

    private static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SpinTurn = TimeSpan.FromMilliseconds(800);
    private static readonly TimeSpan MinSpin = TimeSpan.FromMilliseconds(600);

    /// <summary>How long a card waits for its first read before showing anyway, its page saying it is reading.</summary>
    private static readonly TimeSpan FirstReadWait = TimeSpan.FromMilliseconds(500);

    /// <summary>A touchpad swipe sends a burst of sideways wheel steps: after a page turns, the rest of the burst waits this long.</summary>
    private static readonly TimeSpan SwipeSettles = TimeSpan.FromMilliseconds(450);

    private readonly IReadOnlyList<IUsageProvider> _providers;
    private readonly CardStore _store;
    private readonly CardKind _kind;
    private readonly DispatcherTimer _timer = new() { Interval = RefreshEvery };
    private readonly List<PageView> _columns = [];
    private PageSelection _selection;
    private IReadOnlySet<int> _pagesElsewhere = new HashSet<int>();
    private double _designWidth = SinglePageWidth;
    private double _designHeight;
    private Task? _refresh;
    private int _swipe;
    private long _swipeSettlesAt;
    private bool _isDetailed;
    private bool _placesAtCorner;
    private bool _isQuitting;

    /// <summary>The columns changed while shown: measured again once their pages are filled in.</summary>
    private bool _isMeasureDue;

    /// <summary>What the tray icon reads when hovered: the first page's own numbers, in three lines.</summary>
    public event Action<string>? SummaryChanged;

    /// <summary>All providers' latest reads, including while the card is hidden.</summary>
    public event Action<IReadOnlyList<PetUsage>, DateTimeOffset>? UsageRead;

    /// <summary>"Nova janela" on a page's icon: whoever holds the cards opens one for that provider beside this card.</summary>
    public event Action<MainWindow, string>? NewWindowRequested;

    /// <summary>"Sair do MiniInfos": whoever holds the cards quits them all; with no one listening, this card quits the app.</summary>
    public event Action? QuitRequested;

    /// <summary>An extra card closed for good.</summary>
    public event Action<MainWindow>? Dismissed;

    /// <summary>This card put other pages on screen.</summary>
    public event Action? PagesChanged;

    /// <summary>The gear, or "Configurações" in the card's menu: whoever holds the cards opens the settings window for this one.</summary>
    public event Action<MainWindow>? SettingsRequested;

    /// <summary>The card switched between Resumido and Detalhado.</summary>
    public event Action? ViewChanged;

    /// <summary>Whole numbers rolling up (Detalhado), or the short 1,4B form (Resumido); set, it flips the card's own switch.</summary>
    public bool IsDetailed
    {
        get => _isDetailed;
        set => (value ? DetailedView : CompactView).IsChecked = true;
    }

    /// <summary>The pages this card has on screen, by their place in the provider list.</summary>
    public IReadOnlyList<int> ShownPages => _selection.Shown;

    /// <summary>
    /// The pages other cards have on screen: their icons light here too, so every card's pager says
    /// which pages are open, as one split card's does.
    /// </summary>
    public void LightPagesShownElsewhere(IReadOnlySet<int> pages)
    {
        _pagesElsewhere = pages;
        UpdatePageIcons();
    }

    /// <param name="providers">One page each, in this order.</param>
    /// <param name="preferences">What every card shares: this one stays on top while they say so.</param>
    /// <param name="store">Where this card keeps its settings; the main card's when left out.</param>
    public MainWindow(IReadOnlyList<IUsageProvider> providers, Preferences preferences, CardStore? store = null, CardKind kind = CardKind.Main)
    {
        if (providers.Count == 0) throw new ArgumentException("The card needs a provider to show.", nameof(providers));

        _providers = providers;
        _store = store ?? CardStore.Main;
        _kind = kind;
        InitializeComponent();
        // The artwork's rounded top, inside the card's 1-pixel border: the clip runs past the bottom so only the top corners round.
        HeaderArt.SizeChanged += (_, e) => HeaderArt.Clip = new RectangleGeometry(new Rect(0, 0, e.NewSize.Width, e.NewSize.Height + ArtCornerRadius), ArtCornerRadius, ArtCornerRadius);
        ShowTitle();
        Themes.Changed += OnAppearanceChanged;
        AppFonts.Changed += OnAppearanceChanged;
        Closed += (_, _) =>
        {
            Themes.Changed -= OnAppearanceChanged;
            AppFonts.Changed -= OnAppearanceChanged;
        };
        SetBinding(TopmostProperty, new Binding(nameof(Preferences.IsTopmost)) { Source = preferences });
        TopmostItem.SetBinding(MenuItem.IsCheckedProperty, new Binding(nameof(Preferences.IsTopmost)) { Source = preferences, Mode = BindingMode.TwoWay });

        var settings = _store.Load();
        _selection = PageSelection.Restore(IndexOf(settings?.Page), IndexOf(settings?.BesidePage), providers.Count);
        BuildPageIcons();
        LayOutColumns();
        DismissButton.ToolTip = _kind == CardKind.Main ? "Esconder (volta quando um app acompanhado abrir de novo)" : "Fechar esta janela";
        DismissItem.Header = _kind == CardKind.Main ? "Esconder" : "Fechar esta janela";
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
            // The card's own size at its design width sets the proportions it keeps. A long history never
            // read before takes seconds: the card shows after a moment at most, its page saying it is
            // reading, and measures again once the numbers come.
            var firstRead = RefreshAsync();
            if (await Task.WhenAny(firstRead, Task.Delay(FirstReadWait)) != firstRead) _isMeasureDue = true;
            SizeToContent = SizeToContent.Manual;
            MeasureDesign();

            var saved = _store.Load();
            Resize(saved?.Scale ?? 1);
            Place(_placesAtCorner ? null : saved);
            Opacity = 1;
        };
        Closing += (_, e) =>
        {
            if (_isQuitting)
            {
                _timer.Stop();
                SaveSettings();
                foreach (var provider in _providers) provider.Flush();
                return;
            }

            if (_kind == CardKind.Main)
            {
                // Alt+F4 hides it like the ✕: it keeps waiting for an app it follows to open again.
                e.Cancel = true;
                HideCard();
                return;
            }

            // An extra card closed by hand goes for good: it forgets its settings, so it doesn't come back.
            _timer.Stop();
            _store.Forget();
            Dismissed?.Invoke(this);
        };
    }

    /// <summary>The settings a card opened from this one starts with: one page, same size and view, a step up and to the left.</summary>
    public CardSettings SettingsForNewCard(string providerId) => new(
        Left - CascadeOffset,
        Top - CascadeOffset,
        _designHeight > 0 ? Width / _designWidth : 1,
        _isDetailed,
        providerId);

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

    /// <summary>Back to the size it was made at, moved to the corner if that size no longer fits where it is.</summary>
    public void ResetSize()
    {
        Resize(1);
        if (!IsOnScreen(Left, Top)) Place(saved: null);
        SaveSettings();
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

    /// <summary>Lets the app's shutdown close this card as it is, saved, instead of hiding or forgetting it.</summary>
    public void PrepareToQuit() => _isQuitting = true;

    public void Quit()
    {
        PrepareToQuit();
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
        var shown = _selection;

        // Every provider reads, so each ledger keeps up with its logs; the pages on screen alone may
        // pay for costlier reads, and only they are forced.
        var pages = await Task.WhenAll(_providers.Select((provider, index) =>
            provider.ReadAsync(now, isShown: shown.Shows(index), force: force && shown.Shows(index))));

        for (var index = 0; index < pages.Length; index++)
        {
            var button = (Button)PageIcons.Children[index];
            button.ToolTip = pages[index].Title;
            AutomationProperties.SetName(button, $"Mostrar {pages[index].Title}");
        }

        Show(pages, now);
        UsageRead?.Invoke(pages.Select((page, index) => PetUsage.Of(_providers[index].Id, page, now)).ToArray(), now);
    }

    /// <summary>Draws the pages on screen, one or two.</summary>
    private void Show(UsagePage[] pages, DateTimeOffset now)
    {
        for (var column = 0; column < _columns.Count; column++)
        {
            _columns[column].Show(pages[_selection.Shown[column]], now, CountText);
        }

        var first = pages[_selection.Shown[0]];
        RefreshButton.ToolTip = _selection.IsSplit ? "Atualizar agora as páginas na tela." : first.RefreshHint;
        SummaryChanged?.Invoke($"MiniInfos · {first.Title}\n{_columns[0].LimitsLine}\nHoje {Format.Count(first.Today.Total)} tokens");

        if (_isMeasureDue) FitColumns();
    }

    /// <summary>A count as the chosen view reads it: 1,4B, or every digit.</summary>
    private string CountText(long value) => _isDetailed ? Format.Full(value) : Format.Count(value);

    private int IndexOf(string? providerId)
    {
        for (var index = 0; index < _providers.Count; index++)
        {
            if (_providers[index].Id == providerId) return index;
        }

        return -1;
    }

    /// <summary>Puts other pages on screen: the columns that changed provider start their counts over.</summary>
    private void Select(PageSelection next)
    {
        if (next.SameAs(_selection)) return;

        var previous = _selection;
        _selection = next;
        LayOutColumns();
        for (var column = 0; column < _columns.Count; column++)
        {
            if (column >= previous.Shown.Count || previous.Shown[column] != next.Shown[column]) _columns[column].Restart();
        }

        UpdatePageIcons();
        SaveSettings();
        PagesChanged?.Invoke();
        _ = RefreshAsync();
    }

    /// <summary>
    /// One column per page on screen, a line between them, and the card as wide as they need. A card
    /// already shown keeps its scale and stays on screen as it widens or narrows.
    /// </summary>
    private void LayOutColumns()
    {
        var count = _selection.Shown.Count;
        while (_columns.Count < count) _columns.Add(new PageView());
        if (_columns.Count > count) _columns.RemoveRange(count, _columns.Count - count);

        Columns.Children.Clear();
        for (var column = 0; column < _columns.Count; column++)
        {
            if (column > 0) Columns.Children.Add(ColumnDivider());
            Columns.Children.Add(_columns[column]);
        }

        Pager.Visibility = _providers.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        PreviousPageButton.Visibility = NextPageButton.Visibility = _selection.CanTurn ? Visibility.Visible : Visibility.Hidden;

        var scale = _designHeight > 0 ? Width / _designWidth : 1;
        _designWidth = SinglePageWidth + (count - 1) * (ColumnWidth + ColumnGap);
        Card.Width = _designWidth;
        if (_designHeight <= 0)
        {
            Width = _designWidth;
            return;
        }

        FitColumns(scale);
        _isMeasureDue = true;
    }

    /// <summary>Sizes a shown card to its columns at its scale, kept on screen.</summary>
    private void FitColumns(double? scale = null)
    {
        var keep = scale ?? Width / _designWidth;
        MeasureDesign();
        Resize(keep);
        KeepOnScreen();
        _isMeasureDue = false;
    }

    /// <summary>The card's height at its design width sets the proportions that resizing keeps.</summary>
    private void MeasureDesign()
    {
        Card.Measure(new Size(_designWidth, double.PositiveInfinity));
        _designHeight = Card.DesiredSize.Height;
        MinWidth = _designWidth * MinScale;
        MinHeight = _designHeight * MinScale;
        MaxWidth = _designWidth * MaxScale;
        MaxHeight = _designHeight * MaxScale;
    }

    /// <summary>The app's name, in the capitals the text font wants.</summary>
    private void ShowTitle() => NumericText.Show(AppTitle, "MiniInfos");

    private void OnAppearanceChanged()
    {
        // The text font may want other capitals: the name now, the pages with a read of what is on screen.
        ShowTitle();
        if (_designHeight <= 0) return;
        _ = RefreshAsync();
        var scale = Width / _designWidth;
        Dispatcher.InvokeAsync(() =>
        {
            if (!IsLoaded || _isQuitting) return;
            Card.InvalidateMeasure();
            FitColumns(scale);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>The line between two columns, in the theme's divider color.</summary>
    private static Border ColumnDivider()
    {
        var divider = new Border { Width = 1, Margin = new Thickness((ColumnGap - 1) / 2, 0, (ColumnGap - 1) / 2, 0) };
        divider.SetResourceReference(Border.BackgroundProperty, "Divider");
        return divider;
    }

    private void BuildPageIcons()
    {
        PageIcons.Children.Clear();
        for (var index = 0; index < _providers.Count; index++)
        {
            var page = index;
            var icon = new Path
            {
                Data = TryFindResource($"ProviderIcon.{_providers[index].Id}") as Geometry
                    ?? (Geometry)FindResource("ProviderIcon.default"),
                Width = 12,
                Height = 12,
                Stretch = Stretch.Uniform,
            };
            var button = new Button
            {
                Style = (Style)FindResource("PageArrow"),
                Width = 20,
                Height = 20,
                Content = icon,
                ToolTip = $"Página {index + 1}",
                ContextMenu = PageMenu(page),
            };
            button.Click += (_, _) => Select(PageSelection.Single(page, _providers.Count));
            PageIcons.Children.Add(button);
        }

        UpdatePageIcons();
    }

    /// <summary>
    /// A page icon's right-click menu, as the Claude app's: "Abrir em" splits the card with this page,
    /// goes back to it alone, or opens it in a card of its own.
    /// </summary>
    private ContextMenu PageMenu(int page)
    {
        var split = new MenuItem { Header = "Visualização dividida" };
        split.Click += (_, _) => Select(_selection.SplitWith(page));
        var single = new MenuItem { Header = "Visualização única" };
        single.Click += (_, _) => Select(PageSelection.Single(page, _providers.Count));
        var newWindow = new MenuItem { Header = "Nova janela" };
        newWindow.Click += (_, _) => NewWindowRequested?.Invoke(this, _providers[page].Id);

        var openIn = new MenuItem { Header = "Abrir em" };
        openIn.Items.Add(split);
        openIn.Items.Add(single);
        openIn.Items.Add(new Separator());
        openIn.Items.Add(newWindow);

        var menu = new CardContextMenu();
        menu.Items.Add(openIn);
        menu.Opened += (_, _) =>
        {
            // Splitting from a split card puts this page beside the first; it is offered while that changes something.
            split.Visibility = _providers.Count > 1 && !_selection.SplitWith(page).SameAs(_selection) ? Visibility.Visible : Visibility.Collapsed;
            single.Visibility = _selection.IsSplit ? Visibility.Visible : Visibility.Collapsed;
        };
        return menu;
    }

    private void UpdatePageIcons()
    {
        for (var index = 0; index < PageIcons.Children.Count; index++)
        {
            var icon = (Path)((Button)PageIcons.Children[index]).Content;
            icon.SetResourceReference(Shape.FillProperty, _selection.Shows(index) || _pagesElsewhere.Contains(index) ? "PrimaryText" : "IconOther");
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

        Select(_selection.Turn(Math.Sign(_swipe)));
        _swipe = 0;
        _swipeSettlesAt = Stopwatch.GetTimestamp() + (long)(SwipeSettles.TotalSeconds * Stopwatch.Frequency);
    }

    private void Resize(double scale)
    {
        var clamped = Math.Clamp(scale, MinScale, MaxScale);
        Width = _designWidth * clamped;
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

    /// <summary>A card that grew past the screen's edge moves back inside it, growing toward the free side.</summary>
    private void KeepOnScreen()
    {
        var right = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth;
        var bottom = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
        Left = Math.Max(SystemParameters.VirtualScreenLeft, Math.Min(Left, right - Width));
        Top = Math.Max(SystemParameters.VirtualScreenTop, Math.Min(Top, bottom - Height));
    }

    private bool IsOnScreen(double left, double top) =>
        left >= SystemParameters.VirtualScreenLeft
        && top >= SystemParameters.VirtualScreenTop
        && left + Width <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
        && top + Height <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

    private void SaveSettings()
    {
        if (_designHeight <= 0) return;

        var beside = _selection.IsSplit ? _providers[_selection.Shown[1]].Id : null;
        _store.Save(new CardSettings(Left, Top, Width / _designWidth, _isDetailed, _providers[_selection.Shown[0]].Id, beside));
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
                    _designWidth / _designHeight,
                    (int)Math.Round(_designWidth * MinScale * dpi),
                    (int)Math.Round(_designWidth * MaxScale * dpi));
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

        Select(_selection.Turn(e.Delta < 0 ? 1 : -1));
        e.Handled = true;
    }

    private void OnPageKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Left or Key.Right)) return;

        Select(_selection.Turn(e.Key == Key.Right ? 1 : -1));
        e.Handled = true;
    }

    private void OnPreviousPage(object sender, RoutedEventArgs e) => Select(_selection.Turn(-1));

    private void OnNextPage(object sender, RoutedEventArgs e) => Select(_selection.Turn(1));

    private void OnViewChanged(object sender, RoutedEventArgs e)
    {
        _isDetailed = DetailedView.IsChecked == true;
        SaveSettings();
        ViewChanged?.Invoke();
        _ = RefreshAsync();
    }

    private void OnBackToCorner(object sender, RoutedEventArgs e) => BackToCorner();

    private void OnDefaultSize(object sender, RoutedEventArgs e) => ResetSize();

    private void OnSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this);

    private async void OnRefresh(object sender, RoutedEventArgs e) => await ForceRefreshAsync();

    /// <summary>The ✕: the main card hides, an extra one closes for good.</summary>
    private void OnDismiss(object sender, RoutedEventArgs e)
    {
        if (_kind == CardKind.Main) HideCard();
        else Close();
    }

    private void OnQuit(object sender, RoutedEventArgs e)
    {
        if (QuitRequested is { } quit) quit();
        else Quit();
    }
}
