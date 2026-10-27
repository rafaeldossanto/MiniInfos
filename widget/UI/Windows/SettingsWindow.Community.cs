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
        if (_preferences.PetId is { } pet && !PetAppearances.IsBuiltIn(pet) && PackageLibrary.Shared.Find(PackageKind.Mascot, pet) is null)
            _preferences.PetId = null;
        RefreshCommunity();
        ShowPetPreview();
    }

    /// <summary>Lays out what is installed again: the theme row and library, and the mascot gallery.</summary>
    public void RefreshCommunity(bool refreshLibraries = true)
    {
        _petLibraryRevision++;
        _showingCommunity = true;
        try
        {
            ShowTheme();
            BuildPetChoices();
            ShowCommunitySelection();
        }
        finally { _showingCommunity = false; }
        if (refreshLibraries)
            foreach (var library in OwnedWindows.OfType<ThemeLibraryWindow>()) library.ShowThemes();
    }

    private void BuildPetChoices()
    {
        var panel = CommunityPetChoices;
        panel.Children.Clear();
        panel.Children.Add(AddPetPackage);
        AthenaPet.Content = PetThumbnail(null);
        panel.Children.Add(AthenaPet);
        foreach (var builtin in PetAppearances.BuiltIns.Where(pet => pet.Id is not null))
            panel.Children.Add(PetChoice(PetThumbnail(null, builtin.Id), builtin.Id!, builtin.Appearance.Name, builtin.Appearance.Name + L.Text(" · Incluído no MiniInfos")));
        foreach (var package in PackageLibrary.Shared.List(PackageKind.Mascot))
            panel.Children.Add(PetChoice(PetThumbnail(package), package.Id, package.Name, $"{package.Name}\n{package.Attribution}"));
    }

    private RadioButton PetChoice(Image thumbnail, string id, string name, string tip)
    {
        var button = new RadioButton
        {
            Content = thumbnail, Tag = id, GroupName = "MascotChoice", Style = (Style)FindResource("PetLibraryChoice"), ToolTip = tip,
        };
        AutomationProperties.SetName(button, name);
        button.Checked += (_, _) => { if (!_showingCommunity) _preferences.PetId = id; };
        return button;
    }

    private Image PetThumbnail(InstalledPackage? package, string? builtinId = null)
    {
        var image = new Image { Width = 52, Height = 52 };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        _ = LoadPetImageAsync(image, package, _petLibraryRevision, selected: false, builtinId);
        return image;
    }

    private async Task LoadPetImageAsync(Image image, InstalledPackage? package, int revision, bool selected, string? builtinId = null)
    {
        var source = package is null
            ? await PackageLibrary.Shared.Previews.GetBuiltInAsync(builtinId).ConfigureAwait(false)
            : await PackageLibrary.Shared.Previews.GetAsync(package).ConfigureAwait(false);
        if (Dispatcher.HasShutdownStarted) return;
        await Dispatcher.InvokeAsync(() =>
        {
            if (!_communityClosed && revision == (selected ? _petPreviewRevision : _petLibraryRevision)) image.Source = source;
        });
    }

    private void ShowCommunitySelection()
    {
        foreach (var button in CommunityPetChoices.Children.OfType<RadioButton>()) button.IsChecked = (string)button.Tag == _preferences.PetId;
        AthenaPet.IsChecked = _preferences.PetId is null;
        RemoveThemePackage.IsEnabled = _preferences.CommunityThemeId is not null && !_packageBusy;
        RemovePetPackage.IsEnabled = !PetAppearances.IsBuiltIn(_preferences.PetId) && !_packageBusy;
    }

    private void ShowPetPreview()
    {
        var package = PackageLibrary.Shared.Find(PackageKind.Mascot, _preferences.PetId);
        var builtinId = PetAppearances.IsBuiltIn(_preferences.PetId) ? _preferences.PetId : null;
        PetName.Text = package?.Name ?? PetAppearances.BuiltIn(builtinId)!.Name;
        PetPreview.Source = null;
        _ = LoadPetImageAsync(PetPreview, package, ++_petPreviewRevision, selected: true, builtinId);
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
        var dialog = new OpenFileDialog { Title = kind == PackageKind.Theme ? L.Text("Adicionar tema") : L.Text("Adicionar mascote"), Filter = L.Text("Pacotes do MiniInfos (*.miniinfos;*.zip)|*.miniinfos;*.zip") };
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
            Status(kind).Text = L.Format("{0} instalado · {1}", package.Name, package.Attribution);
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
        var dialog = new SaveFileDialog { Title = L.Text("Exportar pacote para compartilhar ou editar"), Filter = L.Text("Pacote do MiniInfos (*.miniinfos)|*.miniinfos"), DefaultExt = ".miniinfos", FileName = L.Text(kind == PackageKind.Theme ? "meu-tema.miniinfos" : "meu-mascote.miniinfos") };
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
            if (id is not null && !(kind == PackageKind.Mascot && PetAppearances.IsBuiltIn(id)))
                await Task.Run(() => PackageLibrary.Shared.Export(kind, id, file));
            else
            {
                var draft = kind == PackageKind.Theme ? PackageTemplates.Theme(ThemeManager.Load(_preferences))
                    : await PackageTemplates.MascotAsync(appearance: PetAppearances.Load(_preferences.PetId));
                await Task.Run(() => PackageLibrary.Create(file, draft.Manifest, draft.Assets));
            }
            Status(kind).Text = L.Text("Pacote exportado. Para criar sua versão, edite o manifest.json e as imagens antes de compartilhar.");
        });
    }

    private void OnRemoveTheme(object sender, RoutedEventArgs e) => RemovePackage(PackageKind.Theme);
    private void OnRemovePet(object sender, RoutedEventArgs e) => RemovePackage(PackageKind.Mascot);

    private void OnShowPetExample(object sender, RoutedEventArgs e) => ShowOwned(() => new MascotExampleWindow());

    private void RemovePackage(PackageKind kind)
    {
        if (_packageBusy) return;
        var id = kind == PackageKind.Theme ? _preferences.CommunityThemeId : _preferences.PetId;
        if (id is null) return;
        if (kind == PackageKind.Mascot && PetAppearances.IsBuiltIn(id)) return;
        try
        {
            PackageLibrary.Shared.Remove(kind, id);
            if (kind == PackageKind.Theme) _preferences.CommunityThemeId = null;
            else _preferences.PetId = null;
            RefreshCommunity();
            Status(kind).Text = L.Text("Pacote removido.");
        }
        catch (Exception e) when (PackageError(e)) { Status(kind).Text = e.Message; }
    }

    private async Task PackageAction(PackageKind kind, Func<Task> action)
    {
        _packageBusy = true;
        ThemeChoices.IsEnabled = CommunityPetChoices.IsEnabled = AthenaPet.IsEnabled = PetToggle.IsEnabled = false;
        AddThemePackage.IsEnabled = AddPetPackage.IsEnabled = ExportThemePackage.IsEnabled = ExportPetPackage.IsEnabled = false;
        ShowCommunitySelection();
        Status(kind).Text = L.Text("Preparando pacote…");
        try { await action(); }
        catch (Exception e) when (PackageError(e)) { Status(kind).Text = e is PackageException ? e.Message : L.Text("Não foi possível acessar o pacote ou o arquivo de destino."); }
        finally
        {
            _packageBusy = false;
            ThemeChoices.IsEnabled = CommunityPetChoices.IsEnabled = AthenaPet.IsEnabled = PetToggle.IsEnabled = true;
            AddThemePackage.IsEnabled = AddPetPackage.IsEnabled = ExportThemePackage.IsEnabled = ExportPetPackage.IsEnabled = true;
            ShowCommunitySelection();
        }
    }

    private TextBlock Status(PackageKind kind) => kind == PackageKind.Theme ? ThemePackageStatus : PetPackageStatus;
    private static bool PackageError(Exception e) => e is PackageException or IOException or UnauthorizedAccessException or ArgumentException;
}
