using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using MiniInfos.Community;
using MiniInfos.Community.Catalog;
using static MiniInfos.Desktop.UI.Elements;

namespace MiniInfos.Desktop.UI;

/// <summary>Only one page of previews is requested; packages download solely on an Install click.</summary>
internal sealed class CatalogWindow : Window
{
    private const int PageSize = 8;
    private readonly DesktopSession _session;
    private readonly PackageKind _kind;
    private readonly CatalogBrowser _browser = CatalogBrowser.CreateDefault();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly StackPanel _rows = new() { Spacing = 12 };
    private readonly TextBox _search = new();
    private CatalogSnapshot? _catalog;
    private int _page;
    private CancellationTokenSource? _visible;
    public CatalogWindow(DesktopSession session, PackageKind kind)
    {
        _session = session; _kind = kind;
        Title = "MiniInfos · " + L.Text("Catálogo"); Width = 620; Height = 650; MinWidth = 440; MinHeight = 400;
        Background = session.Palette["CardBackground"]; Foreground = session.Palette["PrimaryText"]; FontFamily = session.Palette.TextFont;
        _search.PlaceholderText = L.Text("Buscar pacotes"); _search.TextChanged += (_, _) => { _page = 0; Render(); };
        Content = new ScrollViewer { Content = Stack(_search, _rows), Margin = new Thickness(20) };
        Closed += (_, _) => { _visible?.Cancel(); _cancellation.Cancel(); };
        _ = LoadAsync();
    }
    private async Task LoadAsync()
    {
        _rows.Children.Add(Text(L.Text("Carregando catálogo…"), _session.Palette));
        try { _catalog = await _browser.LoadAsync(false, _cancellation.Token); Render(); }
        catch (Exception e) when (CatalogFailure.IsExpected(e)) { _rows.Children.Clear(); _rows.Children.Add(Text(e.Message, _session.Palette)); }
    }
    private void Render()
    {
        if (_catalog is null || _cancellation.IsCancellationRequested) return;
        _visible?.Cancel(); _visible?.Dispose(); _visible = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token);
        var token = _visible.Token;
        var entries = _catalog.Index.Packages.Where(entry => entry.Kind == _kind && (entry.Name.Contains(_search.Text ?? "", StringComparison.CurrentCultureIgnoreCase)
            || entry.Author.Contains(_search.Text ?? "", StringComparison.CurrentCultureIgnoreCase))).ToArray();
        _page = Math.Clamp(_page, 0, Math.Max(0, (entries.Length - 1) / PageSize));
        _rows.Children.Clear();
        foreach (var entry in entries.Skip(_page * PageSize).Take(PageSize))
        {
            var preview = new Image { Width = 64, Height = 64 };
            _ = LoadPreviewAsync(preview, entry, token);
            var label = Text(entry.Description, _session.Palette, 11, true); label.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
            var state = _browser.State(entry);
            var install = Button(L.Text(state switch { CatalogInstallState.Install => "Instalar", CatalogInstallState.Update => "Atualizar", _ => "Instalado" }), () => { });
            install.IsEnabled = state != CatalogInstallState.Installed;
            install.Click += async (_, _) =>
            {
                install.IsEnabled = false;
                try
                {
                    var package = await _browser.InstallAsync(_catalog, entry, _cancellation.Token);
                    if (_kind == PackageKind.Mascot) _session.Preferences.PetId = package.Id; else _session.Preferences.CommunityThemeId = package.Id;
                    if (!_cancellation.IsCancellationRequested) Render();
                }
                catch (Exception e) when (CatalogFailure.IsExpected(e)) { if (!_cancellation.IsCancellationRequested) { label.Text = e.Message; install.IsEnabled = true; } }
            };
            var info = Stack(Text(entry.Name, _session.Palette, 15), Text(entry.Author + " · " + entry.Version + " · " + entry.License, _session.Palette, 10, true), label, install);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("80,*"), Margin = new Thickness(0, 6) };
            row.Children.Add(preview); Grid.SetColumn(info, 1); row.Children.Add(info); _rows.Children.Add(row);
        }
        var previous = Button(L.Text("Anterior"), () => { _page--; Render(); }); previous.IsEnabled = _page > 0;
        var next = Button(L.Text("Próximo"), () => { _page++; Render(); }); next.IsEnabled = (_page + 1) * PageSize < entries.Length;
        _rows.Children.Add(Row(previous, Text($"{_page + 1} / {Math.Max(1, (entries.Length + PageSize - 1) / PageSize)}", _session.Palette), next));
    }
    private async Task LoadPreviewAsync(Image image, CatalogEntry entry, CancellationToken cancellation)
    {
        try
        {
            var bitmap = await _browser.PreviewAsync(_catalog!, entry, cancellation);
            if (!cancellation.IsCancellationRequested) image.Source = bitmap;
        }
        catch (OperationCanceledException) { }
    }
}
