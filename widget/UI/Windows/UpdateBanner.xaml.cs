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

    public UpdateBanner(MainWindow card, Updater updater, Preferences preferences)
    {
        _card = card;
        _updater = updater;
        InitializeComponent();
        SetBinding(TopmostProperty, new Binding(nameof(Preferences.IsTopmost)) { Source = preferences });

        updater.Changed += Follow;
        card.LocationChanged += (_, _) => Place();
        card.SizeChanged += (_, _) => Follow();
        card.IsVisibleChanged += (_, _) => Follow();
        SizeChanged += (_, _) => Place();
        AppFonts.Changed += Follow;
        Closed += (_, _) =>
        {
            updater.Changed -= Follow;
            AppFonts.Changed -= Follow;
        };
    }

    /// <summary>Shows, words and places the line for where the update stands; hides it with the card.</summary>
    private void Follow()
    {
        if (_updater.Stage == UpdateStage.None || !_card.IsVisible)
        {
            Hide();
            return;
        }

        var version = _updater.Available?.Version.ToString(3);
        var (message, button) = _updater.Stage switch
        {
            UpdateStage.Downloading => ($"Baixando a versão {version}… {_updater.Progress:0%}", "Baixando"),
            UpdateStage.Installing => ("Instalando, o MiniInfos já volta…", "Instalando"),
            UpdateStage.Failed => ("Não deu pra atualizar. Clique pra tentar de novo", "Tentar"),
            _ => ("Nova atualização disponível", "Baixar"),
        };
        NumericText.Show(Message, message);
        NumericText.Show(ButtonText, button);
        DownloadButton.IsEnabled = _updater.Stage is UpdateStage.Available or UpdateStage.Failed;
        Strip.ToolTip = $"MiniInfos {version}. Clique pra atualizar: o MiniInfos fecha e abre de novo, já na versão nova.";

        Width = _card.ActualWidth;
        if (!IsVisible) Show();
        Place();
    }

    /// <summary>Just above the card, the same width; below it when there is no room above.</summary>
    private void Place()
    {
        if (!IsVisible) return;

        var above = _card.Top - ActualHeight - Gap;
        Left = _card.Left;
        Top = above >= SystemParameters.VirtualScreenTop ? above : _card.Top + _card.ActualHeight + Gap;
    }

    private async void OnDownload(object sender, RoutedEventArgs e) => await _updater.InstallAsync();

    private async void OnStripClicked(object sender, MouseButtonEventArgs e) => await _updater.InstallAsync();
}
