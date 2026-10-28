using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using MiniInfos.Community;
using MiniInfos.Desktop.Platform;
using MiniInfos.Infrastructure;
using MiniInfos.Mascot;
using MiniInfos.Settings;
using MiniInfos.Updates;
using static MiniInfos.Desktop.UI.Elements;

namespace MiniInfos.Desktop.UI;

internal sealed class SettingsWindow : Window
{
    private readonly DesktopSession _session;
    private TextBlock _status = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TabControl _tabs = new();
    private readonly CancellationTokenSource _cancellation = new();
    private Palette P => _session.Palette;
    private Preferences Preferences => _session.Preferences;
    public SettingsWindow(DesktopSession session)
    {
        _session = session; Width = 590; Height = 620; MinWidth = 460; MinHeight = 420;
        Content = _tabs; Render();
        _session.Changed += ThemeChanged;
        Closed += (_, _) => { _session.Changed -= ThemeChanged; _cancellation.Cancel(); };
    }
    private string? _presentation;
    private void ThemeChanged()
    {
        var key = $"{Preferences.ThemeKey}:{Preferences.TextFont}:{Preferences.NumberFont}:{L.Culture.Name}:{Preferences.PetEnabled}:{Preferences.PetSize}:{Preferences.PetId}:{Preferences.IsTopmost}:{Preferences.FollowsApps}";
        if (_presentation == key) return;
        _presentation = key; Render();
    }
    internal void Render()
    {
        var selected = Math.Max(0, _tabs.SelectedIndex);
        _status = new TextBlock { Text = _status.Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        Title = "MiniInfos · " + L.Text("Configurações"); Background = P["CardBackground"]; FontFamily = P.TextFont; Foreground = P["PrimaryText"];
        _tabs.Items.Clear();
        AddTab("Geral", General()); AddTab("Aparência", Appearance()); AddTab("Mascote", Pets());
        _tabs.SelectedIndex = Math.Min(selected, 2);
    }
    private void AddTab(string name, Control content) => _tabs.Items.Add(new TabItem { Header = L.Text(name), Content = new ScrollViewer { Content = content, Margin = new Thickness(20) } });
    private StackPanel General()
    {
        var panel = Stack(Text(L.Text("Idioma"), P, 15), Choice(Enum.GetValues<LanguageChoice>(), Preferences.Language,
            value => value switch { LanguageChoice.Automatic => L.Text("Automático (sistema)"), LanguageChoice.Portuguese => "Português", _ => "English" }, value => Preferences.Language = value),
            Check("Sempre por cima", Preferences.IsTopmost, value => Preferences.IsTopmost = value),
            Check("Seguir aplicativos", Preferences.FollowsApps, value => Preferences.FollowsApps = value),
            Check("Iniciar com o sistema", StartupIntegration.IsEnabled, value => Safe(() => StartupIntegration.SetEnabled(value))), Line(P),
            Text("MiniInfos " + AppInfo.Version, P, 14), Button(L.Text("Verificar atualizações"), () => _ = UpdateAsync()),
            Button(L.Text("Abrir pasta de dados"), () => _ = Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(Infrastructure.Storage.AppPaths.Data))),
            Text(L.Text("Seus históricos e contagens ficam neste computador."), P, 12, true), _status);
        panel.Spacing = 16; return panel;
    }
    private StackPanel Appearance()
    {
        var panel = Stack(Text(L.Text("Tema"), P, 15)); panel.Spacing = 14;
        foreach (var choice in Enum.GetValues<ThemeChoice>())
        {
            var label = choice switch { ThemeChoice.Simple => "Simples", ThemeChoice.Classic => "Clássico", ThemeChoice.Light => "Claro", ThemeChoice.System => "Sistema", ThemeChoice.ClassicLight => "Clássico claro", _ => "Clássico escuro" };
            var button = Button(L.Text(label), () => Preferences.Theme = choice);
            button.Opacity = Preferences.CommunityThemeId is null && Preferences.Theme == choice ? 1 : 0.6;
            panel.Children.Add(button);
        }
        panel.Children.Add(Text(L.Text("Fontes"), P, 15));
        panel.Children.Add(Text(L.Text("Textos"), P));
        panel.Children.Add(Choice(Enum.GetValues<FontChoice>(), Preferences.TextFont, value => value.ToString(), value => Preferences.TextFont = value));
        panel.Children.Add(Text(L.Text("Números"), P));
        panel.Children.Add(Choice(Enum.GetValues<FontChoice>(), Preferences.NumberFont, value => value.ToString(), value => Preferences.NumberFont = value));
        Library(panel, PackageKind.Theme);
        return panel;
    }
    private StackPanel Pets()
    {
        var panel = Stack(Check("Mostrar mascote", Preferences.PetEnabled, value => Preferences.PetEnabled = value),
            Choice(Enum.GetValues<PetSizeChoice>(), Preferences.PetSize, value => L.Text(value switch { PetSizeChoice.Small => "Pequeno", PetSizeChoice.Large => "Grande", _ => "Médio" }), value => Preferences.PetSize = value));
        var owl = Button(L.Text("Coruja de Atena"), () => Preferences.PetId = null);
        owl.Opacity = Preferences.PetId is null ? 1 : 0.55; panel.Children.Add(owl);
        Library(panel, PackageKind.Mascot);
        return panel;
    }
    private void Library(StackPanel panel, PackageKind kind)
    {
        panel.Children.Add(Line(P));
        panel.Children.Add(Row(Button("+ " + L.Text("Importar pacote"), () => _ = ImportAsync(kind)), Button(L.Text("Explorar catálogo"), () => new CatalogWindow(_session, kind).Show())));
        foreach (var package in PackageLibrary.Shared.List(kind))
        {
            var selected = kind == PackageKind.Mascot ? Preferences.PetId == package.Id : Preferences.CommunityThemeId == package.Id;
            var use = Button(package.Name, () => { if (kind == PackageKind.Mascot) Preferences.PetId = package.Id; else Preferences.CommunityThemeId = package.Id; });
            use.Opacity = selected ? 1 : 0.55;
            panel.Children.Add(Stack(use, Text(package.Attribution, P, 10, true), Row(
                Button(L.Text("Exportar"), () => _ = ExportAsync(package)), Button(L.Text("Remover"), () => Safe(() =>
                {
                    PackageLibrary.Shared.Remove(kind, package.Id);
                    if (kind == PackageKind.Mascot && selected) Preferences.PetId = null;
                    if (kind == PackageKind.Theme && selected) Preferences.CommunityThemeId = null;
                    Render();
                })))));
        }
    }
    private async Task ImportAsync(PackageKind kind)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = L.Text("Importar pacote"), FileTypeFilter = [new FilePickerFileType("MiniInfos") { Patterns = ["*.miniinfos", "*.zip"] }] });
        if (files.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;
        try { await Task.Run(() => PackageLibrary.Shared.Install(path, kind)); Render(); }
        catch (Exception e) when (e is PackageException or IOException or UnauthorizedAccessException) { await ErrorAsync(e.Message); }
    }
    private async Task ExportAsync(InstalledPackage package)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = package.Id + ".miniinfos", DefaultExtension = "miniinfos" });
        if (file?.TryGetLocalPath() is not { } path) return;
        Safe(() => PackageLibrary.Shared.Export(package.Manifest.Kind, package.Id, path));
    }
    private async Task UpdateAsync()
    {
        _status.Text = L.Text("Verificando atualizações…");
        using var http = GitHubReleases.CreateClient();
        var update = await new GitHubReleases(http, ReleaseAssets.Current).NewerThanAsync(Version.Parse(AppInfo.Version), _cancellation.Token);
        if (_cancellation.IsCancellationRequested) return;
        if (update is null) { _status.Text = L.Text("Nenhuma atualização disponível ou serviço indisponível."); return; }
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = ReleaseAssets.Current });
        if (file?.TryGetLocalPath() is not { } path) return;
        try
        {
            var client = new Infrastructure.Network.GitHubReleaseClient(http);
            var asset = new Infrastructure.Network.GitHubAsset(ReleaseAssets.Current, update.Installer.AbsoluteUri, "sha256:" + update.Sha256, null);
            var bytes = await client.DownloadAsync(asset, 300 * 1024 * 1024, update.Sha256, null, _cancellation.Token);
            await File.WriteAllBytesAsync(path, bytes, _cancellation.Token);
            _status.Text = L.Text("Download verificado. Feche o MiniInfos e instale o pacote baixado.");
        }
        catch (Exception e) when (e is IOException or System.Net.Http.HttpRequestException or OperationCanceledException) { _status.Text = e.Message; }
    }
    private void Safe(Action action) { try { action(); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or PackageException) { _ = ErrorAsync(e.Message); } }
    private Task ErrorAsync(string text)
    {
        var dialog = new Window { Title = "MiniInfos", Width = 440, Height = 190, Content = new TextBlock { Text = text, Margin = new Thickness(24), TextWrapping = Avalonia.Media.TextWrapping.Wrap } };
        return dialog.ShowDialog(this);
    }
    private CheckBox Check(string label, bool value, Action<bool> change)
    {
        var control = new CheckBox { Content = L.Text(label), IsChecked = value };
        control.IsCheckedChanged += (_, _) => change(control.IsChecked == true);
        return control;
    }
    private static ComboBox Choice<T>(T[] values, T selected, Func<T, string> label, Action<T> change)
    {
        var control = new ComboBox { ItemsSource = values.Select(label).ToArray(), SelectedIndex = Array.IndexOf(values, selected), HorizontalAlignment = HorizontalAlignment.Stretch };
        control.SelectionChanged += (_, _) => { if (control.SelectedIndex >= 0) change(values[control.SelectedIndex]); };
        return control;
    }
}
