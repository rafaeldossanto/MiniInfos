using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
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
    private Presentation? _presentation;
    private sealed record Presentation(Palette Palette, string Providers, string Page, string? Beside, bool Detailed, UsagePage? First, UsagePage? Second);
    private double DesignWidth => _state.BesidePage is null ? 300 : 600;
    private double DesignHeight => Colors.HeaderHeight + 290;
    private Palette Colors => _session.Palette;
    private string Selected => _state.Page is { } page && _session.Providers.Any(provider => provider.Id == page) ? page : _session.Providers[0].Id;
    public bool Shows(string providerId) => IsVisible && (Selected == providerId || _state.BesidePage == providerId);

    public CardWindow(DesktopSession session, CardStore store)
    {
        _session = session; _store = store;
        _state = store.Load() ?? new CardSettings(80, 80);
        Title = "MiniInfos"; WindowDecorations = WindowDecorations.None; Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent]; CanResize = true;
        MinWidth = 260; MinHeight = 270;
        Position = new PixelPoint((int)_state.Left, (int)_state.Top);
        Width = (_state.BesidePage is null ? 300 : 600) * Math.Clamp(_state.Scale, 0.8, 2);
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
            _sizing = false; Save();
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
        if (_state.BesidePage is { } removed && !_session.Providers.Any(provider => provider.Id == removed)) _state = _state with { BesidePage = null };
        var presentation = new Presentation(p, string.Join(',', _session.Providers.Select(provider => provider.Id)), Selected, _state.BesidePage,
            _state.IsDetailed, _session.Pages.GetValueOrDefault(Selected), _state.BesidePage is { } beside ? _session.Pages.GetValueOrDefault(beside) : null);
        if (presentation == _presentation) return;
        _presentation = presentation;
        MinWidth = _state.BesidePage is null ? 260 : 520;
        MinHeight = DesignHeight * MinWidth / DesignWidth;
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
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions(_state.BesidePage is null ? "*" : "*,*"), Margin = new Thickness(12, 0) };
        body.Children.Add(Page(Selected));
        if (_state.BesidePage is { } other)
        {
            var second = Page(other); second.Margin = new Thickness(14, 0, 0, 0); Grid.SetColumn(second, 1); body.Children.Add(second);
            var divider = new Border { Width = 1, Background = p["Divider"], HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 4) };
            body.Children.Add(divider);
        }
        body.PointerPressed += (_, e) => _swipe = e.GetPosition(this);
        body.PointerReleased += (_, e) =>
        {
            if (_swipe is { } start && e.GetPosition(this) - start is { } delta && Math.Abs(delta.X) > 50 && Math.Abs(delta.X) > Math.Abs(delta.Y) * 1.5)
                Step(delta.X < 0 ? 1 : -1);
            _swipe = null;
        };
        content.Children.Add(body);
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*"), Margin = new Thickness(9, 8, 9, 6) };
        var tabs = Row();
        if (_state.BesidePage is null) tabs.Children.Add(Button("‹", () => Step(-1), L.Text("Anterior")));
        foreach (var provider in _session.Providers)
        {
            var button = Button(Glyph(provider.Id), () => Select(provider.Id), provider.Id);
            button.Opacity = provider.Id == Selected || provider.Id == _state.BesidePage ? 1 : 0.38;
            button.Background = Brushes.Transparent; button.BorderThickness = new Thickness(0); button.Padding = new Thickness(4, 1);
            Avalonia.Automation.AutomationProperties.SetName(button, provider.Id);
            tabs.Children.Add(button);
        }
        if (_state.BesidePage is null) tabs.Children.Add(Button("›", () => Step(1), L.Text("Próximo")));
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
        border.Width = DesignWidth; border.Height = DesignHeight;
        _menu = Context();
        _menu.Closed += (_, _) => { if (_pendingRender) { _pendingRender = false; Render(); } };
        border.ContextMenu = _menu;
        Content = new Viewbox { Child = border, Stretch = Stretch.Uniform };
        Height = Width * DesignHeight / DesignWidth;
    }
    private StackPanel Page(string id)
    {
        var p = Colors;
        if (!_session.Pages.TryGetValue(id, out var page)) return Stack(Text(id, p, 13), Text(L.Text("Lendo uso…"), p, dim: true));
        var panel = Stack(); panel.Spacing = 9;
        var title = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var label = Text(page.Title, p, 13); label.FontWeight = FontWeight.SemiBold; title.Children.Add(label);
        var note = Text(page.Updated, p, 10, dim: true); Grid.SetColumn(note, 1); title.Children.Add(note); panel.Children.Add(title);
        panel.Children.Add(WindowLine(page.ShortWindow, page.TokensOrigin));
        panel.Children.Add(WindowLine(page.LongWindow, page.TokensOrigin));
        panel.Children.Add(Line(p));
        var counts = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        var today = Stack(Text(L.Text("Tokens hoje"), p, 11, true), Number(page.Today.Total));
        var total = Stack(Text(L.Text("Tokens no total"), p, 11, true), Number(page.Total.Total));
        ToolTip.SetTip(today, Format.Breakdown(page.Today, Count) + "\n" + page.TokensOrigin);
        ToolTip.SetTip(total, Format.Breakdown(page.Total, Count) + "\n" + page.TokensOrigin);
        Grid.SetColumn(total, 1); counts.Children.Add(today); counts.Children.Add(total); panel.Children.Add(counts);
        var activity = Text(page.Activity, p, 11, true); ToolTip.SetTip(activity, page.Activity + "\n" + page.ActivityHint); panel.Children.Add(activity);
        return panel;
    }
    private StackPanel WindowLine(Providers.WindowLine line, string origin)
    {
        var p = Colors; var now = DateTimeOffset.Now;
        var active = line.Window.Limit is { } limit && (limit.ResetsAtTime is not { } reset || reset > now);
        var percent = active ? line.Window.Limit!.PercentUsed : 0;
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(Text(Format.WindowLabel(line.Window.Length), p));
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
            Menu("Visualização dividida", ToggleSplit, _state.BesidePage is not null), open,
            Menu("Números detalhados", () => { _state = _state with { IsDetailed = !_state.IsDetailed }; Save(); Render(); }, _state.IsDetailed),
            Menu("Configurações", _session.OpenSettings), new Separator(), Menu("Fechar esta janela", CloseCard), Menu("Esconder", Hide), Menu("Sair do MiniInfos", _session.Quit),
        }};
    }
    private void Select(string id)
    {
        var split = _state.BesidePage is not null;
        _state = _state with { Page = id, BesidePage = null };
        if (split) Width /= 2;
        Save(); Render();
    }
    private void Step(int direction)
    {
        var ids = _session.Providers.Select(provider => provider.Id).ToArray();
        Select(ids[(Array.IndexOf(ids, Selected) + direction + ids.Length) % ids.Length]);
    }
    internal void ToggleSplit()
    {
        if (_session.Providers.Count < 2) return;
        var beside = _state.BesidePage is null ? _session.Providers.First(provider => provider.Id != Selected).Id : null;
        _state = _state with { BesidePage = beside }; Width = beside is null ? 300 : 600; Save(); Render();
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
        if (!Screens.All.Any(screen => screen.WorkingArea.Contains(Position))) Corner();
    }
    public void Save()
    {
        if (!_ready) return;
        _state = _state with { Left = Position.X, Top = Position.Y, Scale = Width / (_state.BesidePage is null ? 300 : 600) };
        _store.Save(_state);
    }
}
