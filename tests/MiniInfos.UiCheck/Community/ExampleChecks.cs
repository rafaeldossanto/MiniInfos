using MiniInfos.Community;

namespace MiniInfos.UiCheck;

internal static partial class Program
{
    /// <summary>"Exemplo" on the Mascote page: the model's grid and manifest, and a saved model the library accepts.</summary>
    private static void CheckPetExample(SettingsWindow settings, string pictures)
    {
        var button = Find<Button>(settings, "PetExampleButton");
        Check(button.Content as string == "Exemplo" && button.IsVisible, "the Mascote page offers Exemplo beside Exportar and Remover");
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        PumpUntil(() => settings.OwnedWindows.OfType<MascotExampleWindow>().Any(), TimeSpan.FromSeconds(5));
        var example = settings.OwnedWindows.OfType<MascotExampleWindow>().FirstOrDefault();
        Check(example is { IsVisible: true }, "Exemplo opens a window above the settings");
        if (example is null) return;

        try
        {
            PumpUntil(() => example.Ready.IsCompleted, TimeSpan.FromSeconds(20));
            var states = Enum.GetValues<PetState>().Length;
            var labels = Find<StackPanel>(example, "RowLabels").Children.Count;
            var lines = Find<UniformGrid>(example, "GridLines");
            Check(example.Ready.IsCompletedSuccessfully && Find<Image>(example, "SheetImage").Source is { } sheet
                && labels == states && lines.Rows == states && lines.Columns == PetSprites.FrameCount,
                $"it shows the model's grid, a named row per state ({labels} of {states}) and a column per pose ({lines.Columns})");
            var manifest = Find<TextBlock>(example, "ManifestText").Text;
            Check(manifest.Contains("\"schemaVersion\": 1") && manifest.Contains("\"idle\"") && manifest.Contains("\"spriteSheet\": \"sprites.png\""),
                "and a piece of the real manifest");
            Render(example, System.IO.Path.Combine(pictures, "mascot-example.png"));

            var model = System.IO.Path.Combine(pictures, "mascot-example.miniinfos");
            var saving = example.SaveTemplateAsync(model);
            PumpUntil(() => saving.IsCompleted, TimeSpan.FromSeconds(20));
            var library = System.IO.Path.Combine(pictures, "mascot-example-library");
            var installed = new PackageLibrary(library).Install(model, PackageKind.Mascot);
            Check(saving.IsCompletedSuccessfully && installed.Manifest.Mascot!.States.Count == states,
                "Salvar modelo writes a package the + tile accepts, with every state");

            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump(TimeSpan.FromMilliseconds(200));
            Check(settings.OwnedWindows.OfType<MascotExampleWindow>().Count() == 1, "asked again, the same window comes forward");
        }
        finally
        {
            example.Close();
            Pump(TimeSpan.FromMilliseconds(100));
        }
    }
}
