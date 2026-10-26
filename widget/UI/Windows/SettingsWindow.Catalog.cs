using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MiniInfos.Community;
using MiniInfos.Community.Catalog;
using MiniInfos.UI.Controls;

namespace MiniInfos.UI.Windows;

public partial class SettingsWindow
{
    private readonly CatalogBrowser _catalogBrowser;
    private CancellationTokenSource _catalogStop = new();
    private CatalogSnapshot? _catalogSnapshot;
    private PackageKind _catalogKind = PackageKind.Mascot;
    private bool _catalogLoading;
    private bool _catalogInstalling;
    private int _catalogRevision;
    private int _catalogPreviewRevision;
    private CancellationTokenSource _catalogImages = new();
    private readonly List<(CatalogEntry Entry, Image Image, Border Card)> _pendingCatalogImages = [];
    private bool _catalogImagesScheduled;

    private void InitializeCatalog()
    {
        Pages.SelectionChanged += (_, e) =>
        {
            if (e.Source != Pages || _communityClosed) return;
            if (CommunityPage.IsSelected) _ = RefreshCatalogAsync(force: false);
            else _catalogStop.Cancel();
        };
        Closed += (_, _) =>
        {
            _catalogStop.Cancel(); _catalogStop.Dispose();
            _catalogImages.Cancel(); _catalogImages.Dispose();
        };
    }

    public void ShowCommunityPage() => Pages.SelectedItem = CommunityPage;

    /// <summary>The Comunidade page on its Temas, as "Mais temas na Comunidade" in the theme library opens it.</summary>
    public void ShowCommunityThemes()
    {
        CatalogThemes.IsChecked = true;
        ShowCommunityPage();
    }

    public Task RefreshCatalogAsync(bool force = true) => Dispatcher.InvokeAsync(() => LoadCatalogCoreAsync(force)).Task.Unwrap();

    private async Task LoadCatalogCoreAsync(bool force)
    {
        if (_catalogLoading || _catalogInstalling || _communityClosed) return;
        _catalogLoading = true;
        _catalogStop.Cancel();
        _catalogStop.Dispose();
        _catalogStop = new();
        var cancellation = _catalogStop.Token;
        var revision = ++_catalogRevision;
        RefreshCatalog.IsEnabled = false;
        _catalogSnapshot = null;
        CatalogChoices.Children.Clear();
        CatalogStatus.Text = "Carregando catálogo…";
        try
        {
            var snapshot = await _catalogBrowser.LoadAsync(force, cancellation);
            cancellation.ThrowIfCancellationRequested();
            snapshot.Index.Validate();
            if (_communityClosed || revision != _catalogRevision) return;
            _catalogSnapshot = snapshot;
            ShowCatalogChoices();
        }
        catch (OperationCanceledException)
        {
            if (!_communityClosed) CatalogStatus.Text = "Não foi possível carregar a lista agora. Tente atualizar novamente.";
        }
        catch (HttpRequestException e)
        {
            if (!_communityClosed) CatalogStatus.Text = e.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
                ? "Tente de novo em alguns minutos. O limite de consultas do GitHub foi atingido."
                : e.StatusCode == HttpStatusCode.NotFound ? "O catálogo ainda não foi publicado. Você já pode adicionar pacotes baixados nas páginas Mascote e Aparência."
                : "Sem conexão com o catálogo. Confira sua internet e tente atualizar a lista.";
        }
        catch (Exception e) when (CatalogFailure.IsExpected(e))
        {
            if (!_communityClosed) CatalogStatus.Text = "Não foi possível ler o catálogo. Tente atualizar a lista novamente.";
        }
        finally
        {
            _catalogLoading = false;
            if (!_communityClosed) RefreshCatalog.IsEnabled = true;
        }
    }

    private async void OnRefreshCatalog(object sender, RoutedEventArgs e) => await RefreshCatalogAsync();

    private void OnCatalogKindChosen(object sender, RoutedEventArgs e)
    {
        _catalogKind = sender == CatalogThemes ? PackageKind.Theme : PackageKind.Mascot;
        if (_catalogSnapshot is not null) ShowCatalogChoices();
    }

    private void ShowCatalogChoices()
    {
        _catalogImages.Cancel();
        _catalogImages.Dispose();
        _catalogImages = CancellationTokenSource.CreateLinkedTokenSource(_catalogStop.Token);
        _catalogPreviewRevision++;
        _pendingCatalogImages.Clear();
        CatalogChoices.Children.Clear();
        if (_catalogSnapshot is not { } snapshot) return;
        var entries = snapshot.Index.Packages.Where(entry => entry.Kind == _catalogKind).ToArray();
        CatalogStatus.Text = entries.Length == 0 ? "Nenhum pacote disponível nesta categoria." : "Escolha um pacote para instalar no MiniInfos.";
        foreach (var entry in entries)
        {
            var image = PackageCard.Preview();
            var captions = new List<string> { entry.Author, $"{entry.License} · v{entry.Version}" };
            if (!string.IsNullOrWhiteSpace(entry.Description)) captions.Add(entry.Description);
            var state = _catalogBrowser.State(entry);
            var button = new Button
            {
                Content = state switch { CatalogInstallState.Install => "Instalar", CatalogInstallState.Update => "Atualizar", _ => "Instalado" },
                Tag = entry.Id, IsEnabled = state != CatalogInstallState.Installed && !_catalogInstalling,
            };
            var status = PackageCard.Caption(this, "");
            button.Click += async (_, _) => await InstallCatalogEntryAsync(snapshot, entry, button, status);
            var card = PackageCard.Create(this, image, entry.Name, captions, button, status);
            card.Loaded += (_, _) => QueueVisibleCatalogPreviews();
            CatalogChoices.Children.Add(card);
            _pendingCatalogImages.Add((entry, image, card));
        }
        QueueVisibleCatalogPreviews();
    }

    private void OnCatalogScrolled(object sender, ScrollChangedEventArgs e) => QueueVisibleCatalogPreviews();

    private void QueueVisibleCatalogPreviews()
    {
        if (_catalogImagesScheduled || _communityClosed) return;
        _catalogImagesScheduled = true;
        Dispatcher.InvokeAsync(() =>
        {
            _catalogImagesScheduled = false;
            if (!CommunityPage.IsSelected || _catalogImages.IsCancellationRequested || _catalogSnapshot is not { } snapshot) return;
            var viewport = new Rect(0, 0, CatalogScroll.ActualWidth, CatalogScroll.ActualHeight);
            for (var index = _pendingCatalogImages.Count - 1; index >= 0; index--)
            {
                var request = _pendingCatalogImages[index];
                if (!request.Card.IsLoaded || request.Card.ActualHeight == 0) continue;
                var bounds = request.Card.TransformToAncestor(CatalogScroll).TransformBounds(new Rect(request.Card.RenderSize));
                if (!viewport.IntersectsWith(bounds)) continue;
                _pendingCatalogImages.RemoveAt(index);
                _ = LoadCatalogImageAsync(snapshot, request.Entry, request.Image, _catalogPreviewRevision, _catalogImages.Token);
            }
        }, DispatcherPriority.Background);
    }

    private async Task LoadCatalogImageAsync(CatalogSnapshot snapshot, CatalogEntry entry, Image image, int revision, CancellationToken cancellation)
    {
        try
        {
            var source = await _catalogBrowser.PreviewAsync(snapshot, entry, cancellation).ConfigureAwait(false);
            if (Dispatcher.HasShutdownStarted) return;
            await Dispatcher.InvokeAsync(() =>
            {
                if (!_communityClosed && revision == _catalogPreviewRevision) image.Source = source;
            });
        }
        catch (OperationCanceledException) { }
    }

    private Task InstallCatalogEntryAsync(CatalogSnapshot snapshot, CatalogEntry entry, Button button, TextBlock status) =>
        Dispatcher.InvokeAsync(() => InstallCatalogEntryCoreAsync(snapshot, entry, button, status)).Task.Unwrap();

    private async Task InstallCatalogEntryCoreAsync(CatalogSnapshot snapshot, CatalogEntry entry, Button button, TextBlock status)
    {
        if (_catalogInstalling) return;
        _catalogInstalling = true;
        CatalogChoices.IsEnabled = CatalogMascots.IsEnabled = CatalogThemes.IsEnabled = false;
        RefreshCatalog.IsEnabled = false;
        button.IsEnabled = false;
        button.Content = "Instalando…";
        try
        {
            var package = await _catalogBrowser.InstallAsync(snapshot, entry, _catalogStop.Token);
            if (_communityClosed) return;
            SelectPackage(package);
            ShowCatalogChoices();
            CatalogStatus.Text = $"{package.Name} instalado e selecionado.";
        }
        catch (Exception e) when (CatalogFailure.IsExpected(e) || e is OperationCanceledException)
        {
            if (!_communityClosed)
            {
                status.Text = "Não foi possível instalar este pacote. Tente novamente.";
                button.Content = "Tentar novamente";
                button.IsEnabled = true;
            }
        }
        finally
        {
            _catalogInstalling = false;
            if (!_communityClosed)
            {
                RefreshCatalog.IsEnabled = true;
                CatalogChoices.IsEnabled = CatalogMascots.IsEnabled = CatalogThemes.IsEnabled = true;
                // Update all install buttons without discarding a per-package error.
                foreach (var card in CatalogChoices.Children.OfType<Border>())
                    foreach (var action in ((StackPanel)card.Child).Children.OfType<Button>())
                        if (action.Tag is string id && snapshot.Index.Packages.Single(item => item.Id == id) is { } item)
                            action.IsEnabled = _catalogBrowser.State(item) != CatalogInstallState.Installed;
            }
        }
    }
}
