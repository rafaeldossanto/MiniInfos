using System.Windows.Data;
using System.Windows.Input;
using System.Windows;
using MiniInfos.Settings;
using MiniInfos.UI.Controls;
using MiniInfos.UI.Themes;
using MiniInfos.Updates;

namespace MiniInfos.UI.Windows;

/// <summary>
/// The line that says a new version is out: a window of its own just above the main card (below it when
/// the top of the screen is in the way), as wide as the card, shown only with the card and an update.
/// </summary>
public partial class UpdateBanner : Window
{
    private const double Gap = 6;

    private readonly MainWindow _card;
    private readonly Updater _updater;
    private bool _closed;

    public UpdateBanner(MainWindow card, Updater updater, Preferences preferences)
    {
        _card = card;
        _updater = updater;
        InitializeComponent();
        SetBinding(TopmostProperty, new Binding(nameof(Preferences.IsTopmost)) { Source = preferences });

        updater.Changed += Follow;
        card.LocationChanged += OnCardLocationChanged;
        card.SizeChanged += OnCardSizeChanged;
        card.IsVisibleChanged += OnCardVisibilityChanged;
        SizeChanged += (_, _) => Place();
        AppFonts.Changed += Follow;
        L.Current.Changed += OnLanguageChanged;
        Closed += (_, _) =>
        {
            _closed = true;
            updater.Changed -= Follow;
            AppFonts.Changed -= Follow;
            L.Current.Changed -= OnLanguageChanged;
            card.LocationChanged -= OnCardLocationChanged;
            card.SizeChanged -= OnCardSizeChanged;
            card.IsVisibleChanged -= OnCardVisibilityChanged;
        };
    }

    /// <summary>Shows, words and places the line for where the update stands; hides it with the card.</summary>
    private void Follow()
    {
        if (_closed) return;
        if (_updater.Stage == UpdateStage.None || !_card.IsVisible)
        {
            Hide();
            return;
        }

        var version = _updater.Available?.Version.ToString(3);
        var (message, button) = _updater.Stage switch
        {
            UpdateStage.Downloading => (L.Format("Baixando a versão {0}… {1:0%}", version, _updater.Progress), L.Text("Baixando")),
            UpdateStage.Installing => (L.Text("Instalando, o MiniInfos já volta…"), L.Text("Instalando")),
            UpdateStage.Failed => (L.Text("Não deu pra atualizar. Clique pra tentar de novo"), L.Text("Tentar")),
            _ => (L.Text("Nova atualização disponível"), L.Text("Baixar")),
        };
        NumericText.Show(Message, message);
        NumericText.Show(ButtonText, button);
        DownloadButton.IsEnabled = _updater.Stage is UpdateStage.Available or UpdateStage.Failed;
        Strip.ToolTip = L.Format("MiniInfos {0}. Clique pra atualizar: o MiniInfos fecha e abre de novo, já na versão nova.", version);

        Width = _card.ActualWidth;
        if (!IsVisible) Show();
        Place();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Follow();
    private void OnCardLocationChanged(object? sender, EventArgs e) => Place();
    private void OnCardSizeChanged(object sender, SizeChangedEventArgs e) => Follow();
    private void OnCardVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => Follow();

    /// <summary>Just above the card, the same width; below it when there is no room above.</summary>
    private void Place()
    {
        if (_closed || !IsVisible) return;

        var above = _card.Top - ActualHeight - Gap;
        Left = _card.Left;
        Top = above >= SystemParameters.VirtualScreenTop ? above : _card.Top + _card.ActualHeight + Gap;
    }

    private async void OnDownload(object sender, RoutedEventArgs e) => await _updater.InstallAsync();

    private async void OnStripClicked(object sender, MouseButtonEventArgs e) => await _updater.InstallAsync();
}
