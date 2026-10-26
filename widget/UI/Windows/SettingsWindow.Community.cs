using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MiniInfos.Community;
using MiniInfos.Mascot;
using MiniInfos.UI.Themes;

namespace MiniInfos.UI.Windows;

public partial class SettingsWindow
{
    private bool _showingCommunity;
    private bool _packageBusy;
    private bool _communityClosed;
    private int _petLibraryRevision;
    private int _petPreviewRevision;

    private void InitializeCommunity()
    {
        Closed += (_, _) => _communityClosed = true;
        if (_preferences.CommunityThemeId is { } theme && PackageLibrary.Shared.Find(PackageKind.Theme, theme) is null)
            _preferences.CommunityThemeId = null;
        if (_preferences.PetId is { } pet && PackageLibrary.Shared.Find(PackageKind.Mascot, pet) is null)
            _preferences.PetId = null;
        RefreshCommunity();
        ShowTheme();
        ShowPetPreview();
    }

    public void RefreshCommunity()
    {
        _petLibraryRevision++;
        _showingCommunity = true;
        try
        {
            BuildPackageChoices(CommunityThemeChoices, PackageKind.Theme);
            BuildPackageChoices(CommunityPetChoices, PackageKind.Mascot);
            ShowCommunitySelection();
        }
        finally { _showingCommunity = false; }
    }

    private void BuildPackageChoices(Panel panel, PackageKind kind)
    {
        var mascot = kind == PackageKind.Mascot;
        panel.Children.Clear();
        if (mascot)
        {
            panel.Children.Add(AddPetPackage);
            AthenaPet.Content = PetThumbnail(null);
            panel.Children.Add(AthenaPet);
        }
        foreach (var package in PackageLibrary.Shared.List(kind))
        {
            var button = new RadioButton
            {
                Content = mascot ? PetThumbnail(package) : package.Name,
                Tag = package.Id, GroupName = mascot ? "MascotChoice" : "Theme",
                Style = (Style)FindResource(mascot ? "PetLibraryChoice" : "Segment"),
                ToolTip = mascot ? $"{package.Name}\n{package.Attribution}" : package.Attribution,
            };
            if (!mascot) button.Margin = new Thickness(0, 0, 4, 4);
            AutomationProperties.SetName(button, package.Name);
            button.Checked += (_, _) =>
            {
                if (_showingCommunity) return;
                if (kind == PackageKind.Theme) _preferences.CommunityThemeId = package.Id;
                else _preferences.PetId = package.Id;
            };
            panel.Children.Add(button);
        }
    }

    private Image PetThumbnail(InstalledPackage? package)
    {
        var image = new Image { Width = 52, Height = 52 };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        _ = LoadPetImageAsync(image, package, _petLibraryRevision, selected: false);
        return image;
    }

    private async Task LoadPetImageAsync(Image image, InstalledPackage? package, int revision, bool selected)
    {
        var source = await PackageLibrary.Shared.Previews.GetAsync(package).ConfigureAwait(false);
        if (Dispatcher.HasShutdownStarted) return;
        await Dispatcher.InvokeAsync(() =>
        {
            if (!_communityClosed && revision == (selected ? _petPreviewRevision : _petLibraryRevision)) image.Source = source;
        });
    }

    private void ShowCommunitySelection()
    {
        foreach (var button in CommunityThemeChoices.Children.OfType<RadioButton>()) button.IsChecked = (string)button.Tag == _preferences.CommunityThemeId;
        foreach (var button in CommunityPetChoices.Children.OfType<RadioButton>()) button.IsChecked = (string)button.Tag == _preferences.PetId;
        AthenaPet.IsChecked = _preferences.PetId is null;
        RemoveThemePackage.IsEnabled = _preferences.CommunityThemeId is not null && !_packageBusy;
        RemovePetPackage.IsEnabled = _preferences.PetId is not null && !_packageBusy;
    }

    private void ShowPetPreview()
    {
        var package = PackageLibrary.Shared.Find(PackageKind.Mascot, _preferences.PetId);
        PetName.Text = package?.Name ?? PetAppearances.Athena.Name;
        PetPreview.Source = null;
        _ = LoadPetImageAsync(PetPreview, package, ++_petPreviewRevision, selected: true);
    }

    private void OnAthenaChosen(object sender, RoutedEventArgs e)
    {
        if (!_showingCommunity) _preferences.PetId = null;
    }

    private async void OnAddTheme(object sender, RoutedEventArgs e) => await ImportPackage(PackageKind.Theme);
    private async void OnAddPet(object sender, RoutedEventArgs e) => await ImportPackage(PackageKind.Mascot);

    private async Task ImportPackage(PackageKind kind)
    {
        if (_packageBusy) return;
        var dialog = new OpenFileDialog { Title = kind == PackageKind.Theme ? "Adicionar tema" : "Adicionar mascote", Filter = "Pacotes do MiniInfos (*.miniinfos;*.zip)|*.miniinfos;*.zip" };
        if (dialog.ShowDialog(this) != true) return;
        await ImportPackageAsync(dialog.FileName, kind);
    }

    public Task ImportPackageAsync(string file, PackageKind kind) =>
        Dispatcher.InvokeAsync(() => ImportPackageCoreAsync(file, kind)).Task.Unwrap();

    private async Task ImportPackageCoreAsync(string file, PackageKind kind)
    {
        if (_packageBusy) return;
        await PackageAction(kind, async () =>
        {
            var package = await Task.Run(() => PackageLibrary.Shared.Install(file, kind));
            SelectPackage(package);
            Status(kind).Text = $"{package.Name} instalado · {package.Attribution}";
        });
    }

    private void SelectPackage(InstalledPackage package)
    {
        RefreshCommunity();
        if (package.Manifest.Kind == PackageKind.Theme)
        {
            if (_preferences.CommunityThemeId == package.Id) _preferences.RefreshTheme();
            else _preferences.CommunityThemeId = package.Id;
        }
        else
        {
            if (_preferences.PetId == package.Id) _preferences.RefreshPet();
            else _preferences.PetId = package.Id;
        }
    }

    private async void OnExportTheme(object sender, RoutedEventArgs e) => await ExportPackage(PackageKind.Theme);
    private async void OnExportPet(object sender, RoutedEventArgs e) => await ExportPackage(PackageKind.Mascot);

    private async Task ExportPackage(PackageKind kind)
    {
        if (_packageBusy) return;
        var dialog = new SaveFileDialog { Title = "Exportar pacote para compartilhar ou editar", Filter = "Pacote do MiniInfos (*.miniinfos)|*.miniinfos", DefaultExt = ".miniinfos", FileName = kind == PackageKind.Theme ? "meu-tema.miniinfos" : "meu-mascote.miniinfos" };
        if (dialog.ShowDialog(this) != true) return;
        await ExportPackageAsync(dialog.FileName, kind);
    }

    public Task ExportPackageAsync(string file, PackageKind kind) =>
        Dispatcher.InvokeAsync(() => ExportPackageCoreAsync(file, kind)).Task.Unwrap();

    private async Task ExportPackageCoreAsync(string file, PackageKind kind)
    {
        if (_packageBusy) return;
        await PackageAction(kind, async () =>
        {
            var id = kind == PackageKind.Theme ? _preferences.CommunityThemeId : _preferences.PetId;
            if (id is not null) await Task.Run(() => PackageLibrary.Shared.Export(kind, id, file));
            else
            {
                var draft = kind == PackageKind.Theme ? PackageTemplates.Theme(ThemeManager.Load(_preferences)) : await PackageTemplates.MascotAsync();
                await Task.Run(() => PackageLibrary.Create(file, draft.Manifest, draft.Assets));
            }
            Status(kind).Text = "Pacote exportado. Para criar sua versão, edite o manifest.json e as imagens antes de compartilhar.";
        });
    }

    private void OnRemoveTheme(object sender, RoutedEventArgs e) => RemovePackage(PackageKind.Theme);
    private void OnRemovePet(object sender, RoutedEventArgs e) => RemovePackage(PackageKind.Mascot);

    private void RemovePackage(PackageKind kind)
    {
        if (_packageBusy) return;
        var id = kind == PackageKind.Theme ? _preferences.CommunityThemeId : _preferences.PetId;
        if (id is null) return;
        try
        {
            PackageLibrary.Shared.Remove(kind, id);
            if (kind == PackageKind.Theme) _preferences.CommunityThemeId = null;
            else _preferences.PetId = null;
            RefreshCommunity();
            Status(kind).Text = "Pacote removido.";
        }
        catch (Exception e) when (PackageError(e)) { Status(kind).Text = e.Message; }
    }

    private async Task PackageAction(PackageKind kind, Func<Task> action)
    {
        _packageBusy = true;
        CommunityThemeChoices.IsEnabled = CommunityPetChoices.IsEnabled = AthenaPet.IsEnabled = PetToggle.IsEnabled = false;
        AddThemePackage.IsEnabled = AddPetPackage.IsEnabled = ExportThemePackage.IsEnabled = ExportPetPackage.IsEnabled = false;
        ShowCommunitySelection();
        Status(kind).Text = "Preparando pacote…";
        try { await action(); }
        catch (Exception e) when (PackageError(e)) { Status(kind).Text = e is PackageException ? e.Message : "Não foi possível acessar o pacote ou o arquivo de destino."; }
        finally
        {
            _packageBusy = false;
            CommunityThemeChoices.IsEnabled = CommunityPetChoices.IsEnabled = AthenaPet.IsEnabled = PetToggle.IsEnabled = true;
            AddThemePackage.IsEnabled = AddPetPackage.IsEnabled = ExportThemePackage.IsEnabled = ExportPetPackage.IsEnabled = true;
            ShowCommunitySelection();
        }
    }

    private TextBlock Status(PackageKind kind) => kind == PackageKind.Theme ? ThemePackageStatus : PetPackageStatus;
    private static bool PackageError(Exception e) => e is PackageException or IOException or UnauthorizedAccessException or ArgumentException;
}
