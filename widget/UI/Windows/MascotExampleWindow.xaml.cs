using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using MiniInfos.Community;
using MiniInfos.Mascot;

namespace MiniInfos.UI.Windows;

/// <summary>
/// "Exemplo" on the Mascote page: the built-in owl as a package's grid, a row per state with what it is for,
/// a piece of its manifest, and the model to save, draw over and add with the + tile.
/// </summary>
public partial class MascotExampleWindow : DialogWindow
{
    /// <summary>A grid cell on screen: eight columns fit beside the row names.</summary>
    private const double CellSize = 44;

    /// <summary>What each row is for, as the creator's guide says it.</summary>
    private static readonly IReadOnlyDictionary<PetState, string> Uses = new Dictionary<PetState, string>
    {
        [PetState.Idle] = "repouso (obrigatório)",
        [PetState.Active] = "tokens aumentaram ou um toque",
        [PetState.Alert] = "uma janela em 90% ou mais",
        [PetState.Sleep] = "dois minutos sem tokens",
        [PetState.ProtectEyes] = "toque nos olhos",
        [PetState.HeldHead] = "segurando pela cabeça",
        [PetState.HeldLeftWing] = "segurando pela asa esquerda",
        [PetState.HeldRightWing] = "segurando pela asa direita",
        [PetState.HeldFeet] = "segurando pelos pés",
        [PetState.HeldBody] = "segurando pela barriga",
    };

    private MascotSheet? _sheet;

    public MascotExampleWindow()
    {
        InitializeComponent();
        // Laid out once the window shows: the grid takes a moment, and the window says it is on its way.
        Ready = Dispatcher.InvokeAsync(ShowSheetAsync, DispatcherPriority.Background).Task.Unwrap();
    }

    /// <summary>The grid and the manifest are on screen.</summary>
    public Task Ready { get; }

    /// <summary>Writes the model with the grid on screen, as "Salvar modelo" does after asking where.</summary>
    public async Task SaveTemplateAsync(string file)
    {
        if (_sheet is not { } sheet) return;

        SaveTemplateButton.IsEnabled = false;
        Status.Text = "Salvando o modelo…";
        try
        {
            var draft = await PackageTemplates.MascotAsync(sheet);
            await Task.Run(() => PackageLibrary.Create(file, draft.Manifest, draft.Assets));
            Status.Text = "Modelo salvo. Copie, renomeie para .zip, edite e adicione pelo + na página Mascote.";
        }
        catch (Exception e) when (e is PackageException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            Status.Text = "Não foi possível salvar o modelo nesse lugar.";
        }
        finally
        {
            SaveTemplateButton.IsEnabled = true;
        }
    }

    private async Task ShowSheetAsync()
    {
        var sheet = await PackageTemplates.MascotSheetAsync();
        _sheet = sheet;
        var rows = sheet.Tracks.Count;
        SheetImage.Source = sheet.Image;
        SheetImage.Width = GridLines.Width = sheet.Columns * CellSize;
        SheetImage.Height = GridLines.Height = rows * CellSize;
        GridLines.Columns = sheet.Columns;
        GridLines.Rows = rows;
        for (var cell = 0; cell < sheet.Columns * rows; cell++)
        {
            var line = new Border { BorderThickness = new Thickness(0, 0, 1, 1) };
            line.SetResourceReference(Border.BorderBrushProperty, "Divider");
            GridLines.Children.Add(line);
        }

        foreach (var state in sheet.Tracks.OrderBy(pair => pair.Value.Row).Select(pair => pair.Key))
            RowLabels.Children.Add(RowLabel(state));

        ManifestText.Text = Excerpt(PackageTemplates.MascotManifest(sheet));
        Loading.Visibility = Visibility.Collapsed;
        Sheet.Visibility = Visibility.Visible;
        SaveTemplateButton.IsEnabled = true;
    }

    /// <summary>A row's name as the manifest writes it, and what the row is for.</summary>
    private static StackPanel RowLabel(PetState state)
    {
        var label = new StackPanel { Height = CellSize, VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(new TextBlock
        {
            Text = JsonNamingPolicy.CamelCase.ConvertName(state.ToString()),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Margin = new Thickness(0, 6, 0, 0),
        });
        var use = new TextBlock { Text = Uses[state], FontSize = 11 };
        use.SetResourceReference(TextBlock.ForegroundProperty, "DimText");
        label.Children.Add(use);
        return label;
    }

    /// <summary>The model's manifest with two of its states and no anatomy: the shape of it, short enough to read.</summary>
    private static string Excerpt(PackageManifest manifest)
    {
        var mascot = manifest.Mascot!;
        var shown = mascot with
        {
            States = mascot.States.Where(pair => pair.Key is PetState.Idle or PetState.Active).ToDictionary(),
            Anatomy = null,
        };
        return Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(manifest with { Mascot = shown }, PackageManifest.Json));
    }

    private async void OnSaveTemplate(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Salvar o modelo de mascote",
            Filter = "Pacote do MiniInfos (*.miniinfos)|*.miniinfos",
            DefaultExt = ".miniinfos",
            FileName = "meu-mascote.miniinfos",
        };
        if (dialog.ShowDialog(this) == true) await SaveTemplateAsync(dialog.FileName);
    }
}
