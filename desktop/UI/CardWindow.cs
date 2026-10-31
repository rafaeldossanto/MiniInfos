using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Providers;
using MiniInfos.Settings;
using MiniInfos.UI.Formatting;
using static MiniInfos.Desktop.UI.Elements;

namespace MiniInfos.Desktop.UI;

internal sealed class CardWindow : Window
{
    private readonly DesktopSession _session;
    private readonly CardStore _store;
    private CardSettings _state;
    private Point? _swipe;
    private bool _ready;
    private bool _sizing;
    private ContextMenu? _menu;
    private bool _pendingRender;
    private double _designHeight;
    private CardGridLayout _layout = new(1, 1);
    private bool _needsScroll;
    private ScrollViewer? _pagesScroll;
    private Presentation? _presentation;
    private sealed record Presentation(Palette Palette, string Providers, string Selection, bool Detailed, int Columns, double BodyHeight, bool AlwaysChart, UsagePage?[] Pages);
    private double DesignWidth => _layout.Columns * 300 + (_needsScroll ? 16 : 0);
    private double DesignHeight => _designHeight > 0 ? _designHeight : Colors.HeaderHeight + 290;
    private Palette Colors => _session.Palette;
    private PageSelection Selection
    {
        get
        {
            var ids = _session.Providers.Select(provider => provider.Id).ToArray();
            return PageSelection.Restore(_state.SelectedPages.Select(id => Array.IndexOf(ids, id)), ids.Length);
        }
    }
    private string[] SelectedIds => Selection.Shown.Select(index => _session.Providers[index].Id).ToArray();
    private string Selected => SelectedIds[0];
    private PixelRect WorkArea => (Screens.ScreenFromWindow(this) ?? Screens.Primary)?.WorkingArea ?? new PixelRect(0, 0, 1280, 720);
    public bool Shows(string providerId) => IsVisible && SelectedIds.Contains(providerId);

    public CardWindow(DesktopSession session, CardStore store)
    {
        _session = session; _store = store;
        _state = store.Load() ?? new CardSettings(80, 80);
        Title = "MiniInfos"; WindowDecorations = WindowDecorations.None; Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent]; CanResize = true;
        MinWidth = 260; MinHeight = 270;
        Position = new PixelPoint((int)_state.Left, (int)_state.Top);
        Width = 300 * Math.Clamp(_state.Scale, 0.8, 2);
        Render();
        _session.Changed += Render;
        KeyDown += (_, e) => { if (e.Key is Key.Left or Key.Right) { Step(e.Key == Key.Left ? -1 : 1); e.Handled = true; } };
        Opened += (_, _) => { KeepVisible(); _ready = true; };
        PositionChanged += (_, _) => { if (_ready) Save(); };
        SizeChanged += (_, _) =>
        {
            if (!_ready || _sizing) return;
            _sizing = true;
            Height = Width * DesignHeight / DesignWidth;
            _sizing = false; Save(); Render();
        };
        Closing += (_, e) =>
        {
            if (ReferenceEquals(_store, CardStore.Main) && !_session.IsQuitting
                && e.CloseReason is WindowCloseReason.WindowClosing or WindowCloseReason.Undefined)
            { e.Cancel = true; Hide(); }
        };
        Closed += (_, _) => { _session.Changed -= Render; Save(); };
    }

    internal void Render()
    {
        if (_menu?.IsOpen == true) { _pendingRender = true; return; }
        var p = Colors;
        var selected = SelectedIds;
        var scale = Math.Clamp(_state.Scale, 0.8, 2);
        var layout = CardGridLayout.Fit(selected.Length, (WorkArea.Width / RenderScaling - 32) / scale - 16, 300, 300);
        var bodyHeight = Math.Max(120, (WorkArea.Height / RenderScaling - 32) / scale - p.HeaderHeight - 64);
        var presentation = new Presentation(p, string.Join(',', _session.Providers.Select(provider => provider.Id)), string.Join(',', selected),
            _state.IsDetailed, layout.Columns, bodyHeight, _session.Preferences.AlwaysShowUsageChart, selected.Select(id => _session.Pages.GetValueOrDefault(id)).ToArray());
        if (_presentation is { } prior && presentation.Palette == prior.Palette && presentation.Providers == prior.Providers
            && presentation.Selection == prior.Selection && presentation.Detailed == prior.Detailed && presentation.Columns == prior.Columns
            && presentation.BodyHeight == prior.BodyHeight && presentation.AlwaysChart == prior.AlwaysChart && presentation.Pages.SequenceEqual(prior.Pages)) return;
        _presentation = presentation;
        var scrollOffset = _pagesScroll?.Offset ?? default;
        _layout = layout;
        MinWidth = DesignWidth * 0.8;
        Topmost = _session.Preferences.IsTopmost;
        var content = new StackPanel();
        var header = new Grid { Height = p.HeaderHeight, Margin = new Thickness(0, 0, 0, p.Artwork is null ? 0 : 6) };
        if (p.Artwork is { } art)
        {
            var image = new Image { Source = art, Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Stretch };
            if (p.Inset) { image.Margin = new Thickness(10, 29, 10, 2); image.ClipToBounds = true; }
            header.Children.Add(image);
        }
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(12, 3), VerticalAlignment = VerticalAlignment.Top };
        var name = Text("MiniInfos", p, p.TitleSize); name.FontWeight = FontWeight.SemiBold;
        heading.Children.Add(name);
        var refresh = Button("↻", () => _ = _session.RefreshAsync(true), L.Text("Atualizar agora"));
        var close = Button("×", CloseCard, L.Text("Fechar esta janela"));
        foreach (var control in new[] { refresh, close }) { control.Background = Brushes.Transparent; control.BorderThickness = new Thickness(0); }
        Grid.SetColumn(refresh, 1); Grid.SetColumn(close, 2); heading.Children.Add(refresh); heading.Children.Add(close);
        header.Children.Add(heading);
        header.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.Source is not Avalonia.Controls.Button && e.Source is not Avalonia.Controls.Presenters.ContentPresenter)
                BeginMoveDrag(e);
        };
        content.Children.Add(header);
        var body = new Grid();
        for (var column = 0; column < layout.Columns; column++)
        {
            if (column > 0) body.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(24)));
            body.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(276)));
        }
        for (var row = 0; row < layout.Rows; row++)
        {
            if (row > 0) body.RowDefinitions.Add(new RowDefinition(new GridLength(24)));
            body.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
        var pageContents = selected.Select(Page).ToArray();
        foreach (var page in pageContents) page.Measure(new Size(276, double.PositiveInfinity));
        var charts = UsageChartLayout.Fit(pageContents.Select(page => page.DesiredSize.Height).ToArray(), layout.Columns, _session.Preferences.AlwaysShowUsageChart);
        for (var index = 0; index < selected.Length; index++)
        {
            var page = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
            page.Children.Add(pageContents[index]);
            if (charts[index]) page.Children.Add(new UsageChart(_session.Pages.GetValueOrDefault(selected[index])?.History, p, Count));
            Grid.SetColumn(page, index % layout.Columns * 2); Grid.SetRow(page, index / layout.Columns * 2); body.Children.Add(page);
        }
        for (var column = 1; column < layout.Columns; column++)
        {
            var divider = new Border { Width = 1, Background = p["Divider"], HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(divider, column * 2 - 1); Grid.SetRowSpan(divider, layout.Rows * 2 - 1); body.Children.Add(divider);
        }
        for (var row = 1; row < layout.Rows; row++)
        {
            var divider = new Border { Height = 1, Background = p["Divider"], VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(divider, row * 2 - 1); Grid.SetColumnSpan(divider, layout.Columns * 2 - 1); body.Children.Add(divider);
        }
        body.PointerPressed += (_, e) => _swipe = e.GetPosition(this);
        body.PointerReleased += (_, e) =>
        {
            if (_swipe is { } start && e.GetPosition(this) - start is { } delta && Math.Abs(delta.X) > 50 && Math.Abs(delta.X) > Math.Abs(delta.Y) * 1.5)
                Step(delta.X < 0 ? 1 : -1);
            _swipe = null;
        };
        body.Measure(new Size(layout.Columns * 300 - 24, double.PositiveInfinity));
        _needsScroll = body.DesiredSize.Height > bodyHeight;
        MinWidth = DesignWidth * 0.8;
        _pagesScroll = null;
        if (_needsScroll)
        {
            _pagesScroll = new ScrollViewer { Content = body, Height = bodyHeight, Margin = new Thickness(12, 0),
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
            content.Children.Add(_pagesScroll);
        }
        else
        {
            body.Margin = new Thickness(12, 0);
            content.Children.Add(body);
        }
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*"), Margin = new Thickness(9, 8, 9, 6) };
        var tabs = Row();
        if (Selection.CanTurn) tabs.Children.Add(Button("‹", () => Step(-1), L.Text("Anterior")));
        foreach (var provider in _session.Providers)
        {
            var button = Button(Glyph(provider.Id), () => Select(provider.Id), provider.Id);
            if (provider.Id == "antigravity") button.Content = new PathIcon { Data = Geometry.Parse(AntigravitySymbol), Width = 14, Height = 14, Foreground = p["PrimaryText"] };
            button.Opacity = selected.Contains(provider.Id) ? 1 : 0.38;
            button.Background = Brushes.Transparent; button.BorderThickness = new Thickness(0); button.Padding = new Thickness(4, 1);
            Avalonia.Automation.AutomationProperties.SetName(button, provider.Id);
            var pageIndex = _session.Providers.ToList().IndexOf(provider);
            var add = Menu("Adicionar à visualização", () => SetSelection(Selection.SplitWith(pageIndex)));
            add.IsVisible = !Selection.SplitWith(pageIndex).SameAs(Selection);
            var remove = Menu("Remover desta visualização", () => SetSelection(Selection.Remove(pageIndex)));
            remove.IsVisible = Selection.IsSplit && selected.Contains(provider.Id);
            button.ContextMenu = new ContextMenu { Items = { add, remove,
                Menu("Visualização única", () => Select(provider.Id)), Menu("Nova janela", () => _session.NewCard(provider.Id)) } };
            tabs.Children.Add(button);
        }
        if (Selection.CanTurn) tabs.Children.Add(Button("›", () => Step(1), L.Text("Próximo")));
        foreach (var control in tabs.Children.OfType<Button>()) { control.Background = Brushes.Transparent; control.BorderThickness = new Thickness(0); control.Padding = new Thickness(4, 1); }
        Grid.SetColumn(tabs, 1); footer.Children.Add(tabs);
        var tools = Row(Button("☰", _session.OpenSettings, L.Text("Configurações")), Button("▱", ToggleSplit, L.Text("Visualização dividida")));
        Grid.SetColumn(tools, 2); tools.HorizontalAlignment = HorizontalAlignment.Right; footer.Children.Add(tools);
        content.Children.Add(footer);
        var layers = new Grid(); layers.Children.Add(content);
        var resize = new Border { Width = 12, Height = 12, Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom, Cursor = new Cursor(StandardCursorType.BottomRightCorner) };
        resize.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            BeginResizeDrag(WindowEdge.SouthEast, e); e.Handled = true;
        };
        layers.Children.Add(resize);
        var border = new Border { Background = p["CardBackground"], BorderBrush = p["CardBorder"], BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), ClipToBounds = true, Child = layers };
        border.Width = DesignWidth;
        // Attach first so the ScrollViewer's theme template participates in measurement.
        Content = new Viewbox { Child = border, Stretch = Stretch.Uniform };
        border.Measure(new Size(DesignWidth, double.PositiveInfinity));
        _designHeight = Math.Max(Math.Min(Colors.HeaderHeight + 290, WorkArea.Height / RenderScaling / scale - 32), border.DesiredSize.Height);
        border.Height = DesignHeight;
        MinHeight = DesignHeight * MinWidth / DesignWidth;
        _menu = Context();
        _menu.Closed += (_, _) => { if (_pendingRender) { _pendingRender = false; Render(); } };
        border.ContextMenu = _menu;
        _sizing = true;
        Width = DesignWidth * scale;
        Height = DesignHeight * scale;
        _sizing = false;
        if (_pagesScroll is { } scroll && scrollOffset.Y > 0)
            Dispatcher.UIThread.Post(() => { if (ReferenceEquals(scroll, _pagesScroll)) scroll.Offset = scrollOffset; }, DispatcherPriority.Loaded);
        if (_ready) KeepVisible();
    }
    private StackPanel Page(string id)
    {
        var p = Colors;
        if (!_session.Pages.TryGetValue(id, out var page)) return Stack(Text(id, p, 13), Text(L.Text("Lendo uso…"), p, dim: true));
        var panel = Stack(); panel.Spacing = 9;
        var title = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var label = Text(page.Title, p, 13); label.FontWeight = FontWeight.SemiBold; title.Children.Add(label);
        var note = Text(page.Updated, p, 10, dim: true); Grid.SetColumn(note, 1); title.Children.Add(note); panel.Children.Add(title);
        if (page.QuotaGroups is { } groups)
        {
            foreach (var group in groups) panel.Children.Add(QuotaGroup(group));
            if (groups.Count == 0) panel.Children.Add(Text(page.Activity, p, 11, true));
        }
        else
        {
            panel.Children.Add(WindowLine(page.ShortWindow, page.TokensOrigin));
            panel.Children.Add(WindowLine(page.LongWindow, page.TokensOrigin));
        }
        panel.Children.Add(Line(p));
        var counts = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        var today = Stack(Text(L.Text("Tokens hoje"), p, 11, true), Number(page.Today.Total));
        var total = Stack(Text(L.Text("Tokens no total"), p, 11, true), Number(page.Total.Total));
        ToolTip.SetTip(today, Format.Breakdown(page.Today, Count) + "\n" + page.TokensOrigin);
        ToolTip.SetTip(total, Format.Breakdown(page.Total, Count) + "\n" + page.TokensOrigin);
        Grid.SetColumn(total, 1); counts.Children.Add(today); counts.Children.Add(total); panel.Children.Add(counts);
        if (page.QuotaGroups is null)
        {
            var activity = Text(page.Activity, p, 11, true); ToolTip.SetTip(activity, page.Activity + "\n" + page.ActivityHint); panel.Children.Add(activity);
        }
        return panel;
    }

    private StackPanel QuotaGroup(UsageQuotaGroup group)
    {
        var p = Colors; var now = DateTimeOffset.Now;
        var panel = Stack(); panel.Spacing = 3;
        var separator = Line(p); separator.Margin = new Thickness(0, 1, 0, 3); panel.Children.Add(separator);
        var title = Text(group.Title, p, 13); title.FontWeight = FontWeight.SemiBold; panel.Children.Add(title);
        if (!string.IsNullOrEmpty(group.Models)) panel.Children.Add(Text(group.Models, p, 10, true));
        var meters = new Grid();
        foreach (var window in group.Windows)
        {
            var index = meters.Children.Count;
            meters.ColumnDefinitions.Add(new ColumnDefinition());
            var active = window.Limit is { } limit && (limit.ResetsAtTime is not { } at || at > now);
            var used = active ? Math.Clamp(window.Limit!.PercentUsed, 0, 100) : 0;
            var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            heading.Children.Add(Text(window.Length == TimeSpan.FromDays(1) ? L.Text("Diário") : Format.WindowLabel(window.Length), p, 11));
            var percent = Text(active ? Format.Percent(used) : "—", p, 11, number: true); Grid.SetColumn(percent, 1); heading.Children.Add(percent);
            var bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = used, Height = 3, Background = p["Track"], Foreground = p[used >= 90 ? "Danger" : used >= 70 ? "Warning" : "Accent"] };
            var detail = active ? L.Format("{0} restante", Format.Percent(100 - used)) : L.Text("Limite indisponível");
            if (window.Limit?.ResetsAtTime is { } reset) detail += " · " + Format.Reset(reset, now, remaining: true);
            var meter = Stack(heading, bar); meter.Spacing = 3;
            if (group.Windows.Count == 1) meter.Children.Add(Text(detail, p, 10, true));
            ToolTip.SetTip(meter, detail);
            meter.Margin = new Thickness(index == 0 ? 0 : 6, 0, index < group.Windows.Count - 1 ? 6 : 0, 0);
            Grid.SetColumn(meter, index); meters.Children.Add(meter);
        }
        panel.Children.Add(meters); return panel;
    }
    private StackPanel WindowLine(Providers.WindowLine line, string origin)
    {
        var p = Colors; var now = DateTimeOffset.Now;
        var active = line.Window.Limit is { } limit && (limit.ResetsAtTime is not { } reset || reset > now);
        var percent = active ? line.Window.Limit!.PercentUsed : 0;
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(Text(line.Label ?? Format.WindowLabel(line.Window.Length), p));
        var value = Text(active ? Format.Percent(percent) : "—", p, number: true); Grid.SetColumn(value, 1); heading.Children.Add(value);
        var bar = new ProgressBar { Value = Math.Clamp(percent, 0, 100), Minimum = 0, Maximum = 100, Height = 5,
            Background = p["Track"], Foreground = p[percent >= 90 ? "Danger" : percent >= 70 ? "Warning" : "Accent"] };
        var detail = line.Window.Limit is null ? L.Format("{0} tokens nas {1}", Count(line.Tokens.Total), Format.WindowSpan(line.Window.Length))
            : Count(line.Tokens.Total) + " tokens" + (line.Window.Limit.ResetsAtTime is { } time ? " · " + Format.Reset(time, now) : "");
        var text = Text(detail, p, 10, dim: true, number: true); ToolTip.SetTip(text, Format.Breakdown(line.Tokens, Count) + "\n" + origin);
        return Stack(heading, bar, text);
    }
    private string Count(long value) => _state.IsDetailed ? Format.Full(value) : Format.Count(value);
    private Control Number(long value)
    {
        var text = Text(Count(value), Colors, 18, number: true); text.FontWeight = FontWeight.SemiBold;
        return new Viewbox { Child = text, Height = 25, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left };
    }
    // Official silhouette, matching UI/Icons/ProviderIcons.xaml: https://antigravity.google/favicon.svg
    private const string AntigravitySymbol = "M89.6992 93.695C94.3659 97.195 101.366 94.8617 94.9492 88.445C75.6992 69.7783 79.7825 18.445 55.8659 18.445C31.9492 18.445 36.0325 69.7783 16.7825 88.445C9.78251 95.445 17.3658 97.195 22.0325 93.695C40.1159 81.445 38.9492 59.8617 55.8659 59.8617C72.7825 59.8617 71.6159 81.445 89.6992 93.695Z";
    private static string Glyph(string id) => id switch { "claude" => "✳", "codex" => "◇", "gemini" => "✦", _ => "◈" };
    private ContextMenu Context()
    {
        var open = new MenuItem { Header = L.Text("Abrir em") };
        foreach (var provider in _session.Providers) open.Items.Add(Menu(provider.Id, () => _session.NewCard(provider.Id)));
        return new ContextMenu { Items =
        {
            Menu("Atualizar agora", () => _ = _session.RefreshAsync(true)),
            Menu("Sempre por cima", () => _session.Preferences.IsTopmost = !_session.Preferences.IsTopmost, _session.Preferences.IsTopmost),
            Menu("Voltar pro canto", Corner), Menu("Tamanho padrão", ResetSize),
            Menu("Visualização dividida", ToggleSplit, Selection.IsSplit), open,
            Menu("Números detalhados", () => { _state = _state with { IsDetailed = !_state.IsDetailed }; Save(); Render(); }, _state.IsDetailed),
            Menu("Configurações", _session.OpenSettings), new Separator(), Menu("Fechar esta janela", CloseCard), Menu("Esconder", Hide), Menu("Sair do MiniInfos", _session.Quit),
        }};
    }
    private void Select(string id)
    {
        SetSelection(PageSelection.Single(Array.FindIndex(_session.Providers.ToArray(), provider => provider.Id == id), _session.Providers.Count));
    }
    private void Step(int direction)
    {
        SetSelection(Selection.Turn(direction));
    }
    internal void ToggleSplit()
    {
        if (_session.Providers.Count < 2) return;
        SetSelection(Selection.IsSplit ? PageSelection.Single(Selection.Shown[0], _session.Providers.Count) : Selection.SplitWith(Selection.Shown[0]));
    }
    internal void SetSelection(PageSelection selection)
    {
        var ids = selection.Shown.Select(index => _session.Providers[index].Id).ToArray();
        _state = _state with { Page = ids[0], BesidePage = ids.ElementAtOrDefault(1), Pages = ids };
        Render(); Save();
    }
    private void ResetSize() { Width = DesignWidth; Height = DesignHeight; Save(); }
    private void CloseCard() { if (ReferenceEquals(_store, CardStore.Main)) Hide(); else Close(); }
    private void Corner()
    {
        var area = Screens.ScreenFromWindow(this)?.WorkingArea ?? new PixelRect(0, 0, 1280, 720);
        Position = new PixelPoint(area.Right - (int)(Width * RenderScaling) - 16, area.Bottom - (int)(Height * RenderScaling) - 16);
    }
    private void KeepVisible()
    {
        var area = WorkArea;
        Position = new PixelPoint(Math.Clamp(Position.X, area.X, Math.Max(area.X, area.Right - (int)(Width * RenderScaling))),
            Math.Clamp(Position.Y, area.Y, Math.Max(area.Y, area.Bottom - (int)(Height * RenderScaling))));
    }
    public void Save()
    {
        if (!_ready) return;
        var ids = SelectedIds;
        _state = _state with { Left = Position.X, Top = Position.Y, Scale = Width / DesignWidth,
            Page = ids[0], BesidePage = ids.ElementAtOrDefault(1), Pages = ids };
        _store.Save(_state);
    }
}
