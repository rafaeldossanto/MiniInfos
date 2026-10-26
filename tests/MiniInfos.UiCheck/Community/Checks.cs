using MiniInfos.Community;

namespace MiniInfos.UiCheck;

internal static partial class Program
{
    private static void CheckCommunity(CardSet cards, Preferences preferences, SettingsWindow settings, string pictures)
    {
        void Complete(Task task)
        {
            PumpUntil(() => task.IsCompleted, TimeSpan.FromSeconds(60));
            Check(task.IsCompletedSuccessfully, "the package operation completes without blocking the UI");
            if (task.IsCompleted) task.GetAwaiter().GetResult();
        }
        var originalTheme = preferences.Theme;
        var originalPet = preferences.PetId;
        var originalEnabled = preferences.PetEnabled;
        var originalTextFont = preferences.TextFont;
        var originalNumberFont = preferences.NumberFont;
        const string themeId = "test.community-ocean";
        const string petId = "test.community-pet";
        try
        {
            var themeArchive = System.IO.Path.Combine(pictures, "ocean.miniinfos");
            var theme = new PackageManifest(1, PackageKind.Theme, themeId, "Mar da comunidade", "Criador de teste", "1.0.0", "CC0-1.0",
                Theme: new(Colors: new() { ["Accent"] = "#32C8C1", ["CardBackground"] = "#12272F" }));
            PackageLibrary.Create(themeArchive, theme, new Dictionary<string, byte[]>());
            Complete(settings.ImportPackageAsync(themeArchive, PackageKind.Theme));
            Check(preferences.CommunityThemeId == themeId && Preferences.Load().CommunityThemeId == themeId,
                "importing a theme selects and persists its package ID");
            Check(Find<WrapPanel>(settings, "CommunityThemeChoices").Children.OfType<RadioButton>()
                .Any(choice => (string)choice.Tag == themeId && choice.IsChecked == true), "the imported theme appears selected with creator attribution");
            var face = Find<Border>(cards.Main, "Card");
            Check(face.Background is SolidColorBrush { Color: var color } && color == Color.FromRgb(0x12, 0x27, 0x2F),
                "the community palette applies to the actual card");
            Check(preferences.TextFont == originalTextFont && preferences.NumberFont == originalNumberFont,
                "community themes preserve independently chosen fonts");
            Find<TabControl>(settings, "Pages").SelectedIndex = 1;
            Pump(TimeSpan.FromMilliseconds(300));
            Render(settings, System.IO.Path.Combine(pictures, "community-themes.png"));
            Render(cards.Main, System.IO.Path.Combine(pictures, "community-card.png"));

            var shared = System.IO.Path.Combine(pictures, "ocean-shared.miniinfos");
            Complete(settings.ExportPackageAsync(shared, PackageKind.Theme));
            Check(File.Exists(shared), "installed community themes export for sharing");
            Find<Button>(settings, "RemoveThemePackage").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Check(preferences.CommunityThemeId is null && PackageLibrary.Shared.Find(PackageKind.Theme, themeId) is null,
                "removing the active theme returns to the built-in palette");

            preferences.Theme = ThemeChoice.Classic;
            var themeModel = System.IO.Path.Combine(pictures, "theme-template.miniinfos");
            Complete(settings.ExportPackageAsync(themeModel, PackageKind.Theme));
            var exportedTheme = PackageLibrary.Shared.Install(themeModel, PackageKind.Theme);
            Check(exportedTheme.Manifest.Theme!.HeaderImage is not null, "a built-in illustrated theme exports its header as a local PNG");
            settings.RefreshCommunity();
            Find<WrapPanel>(settings, "CommunityThemeChoices").Children.OfType<RadioButton>().Single(choice => (string)choice.Tag == exportedTheme.Id).IsChecked = true;
            Pump(TimeSpan.FromMilliseconds(350));
            Check(Find<Image>(cards.Main, "AthenaArtwork").Visibility == Visibility.Visible && preferences.CommunityThemeId == exportedTheme.Id,
                "a community theme can supply the illustrated header");
            Find<RadioButton>(settings, "SimpleTheme").IsChecked = true;
            Check(preferences.CommunityThemeId is null, "a built-in theme cleanly replaces the community selection");

            var modelArchive = System.IO.Path.Combine(pictures, "mascot-template.miniinfos");
            preferences.PetId = null;
            Complete(settings.ExportPackageAsync(modelArchive, PackageKind.Mascot));
            Check(File.Exists(modelArchive), "the built-in mascot exports an editable PNG grid and anatomy model");
            var model = PackageLibrary.Shared.Install(modelArchive, PackageKind.Mascot);
            Check(model.Manifest.Mascot!.States.Count == Enum.GetValues<PetState>().Length,
                "the mascot model contains all animation and interaction states");
            var exportedAppearance = PetAppearances.Load(model.Id);
            Check(Enum.GetValues<PetState>().All(state => SameSilhouette(state, 0) && SameSilhouette(state, PetSprites.FrameCount - 1)),
                "exported animation cells preserve the original silhouettes across all state rows");
            // Repackage the exported assets as a creator's own package, just as the authoring guide describes.
            var source = System.IO.Path.Combine(model.Directory, model.Manifest.Mascot.SpriteSheet);
            var petArchive = System.IO.Path.Combine(pictures, "community-friend.miniinfos");
            PackageLibrary.Create(petArchive, model.Manifest with { Id = petId, Name = "Companheiro da comunidade", Author = "Criador de teste" },
                new Dictionary<string, byte[]> { [model.Manifest.Mascot.SpriteSheet] = File.ReadAllBytes(source) });
            preferences.PetEnabled = true;
            var before = cards.Pet;
            Complete(settings.ImportPackageAsync(petArchive, PackageKind.Mascot));
            Pump(TimeSpan.FromMilliseconds(350));
            Check(preferences.PetId == petId && Preferences.Load().PetId == petId && cards.Pet != before,
                "importing a mascot replaces the enabled window and persists its selection");
            Check(cards.Pet is { IsVisible: true } pet && pet.Title.StartsWith("Companheiro da comunidade")
                && Find<TextBlock>(settings, "PetName").Text == "Companheiro da comunidade",
                "the selected package supplies the desktop mascot and settings preview");
            settings.ShowPetPage();
            Pump(TimeSpan.FromMilliseconds(300));
            Render(settings, System.IO.Path.Combine(pictures, "community-mascots.png"));
            CheckPetInteractions(cards.Pet!, pictures);
            Find<Button>(settings, "RemovePetPackage").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump(TimeSpan.FromMilliseconds(350));
            Check(preferences.PetId is null && cards.Pet?.Title.StartsWith("Coruja de Atena") == true,
                "removing a custom mascot restores Athena without disabling the companion");
            var bad = System.IO.Path.Combine(pictures, "invalid-package.miniinfos");
            File.WriteAllText(bad, "invalid zip");
            Complete(settings.ImportPackageAsync(bad, PackageKind.Theme));
            Check(preferences.CommunityThemeId is null && Find<TextBlock>(settings, "ThemePackageStatus").Text.Contains("pacote"),
                "an invalid package shows a useful error and preserves the active appearance");

            bool SameSilhouette(PetState state, int index)
            {
                var expected = PetSprites.Frame(state, index).Pixels.Span;
                var actual = exportedAppearance.Frame(state, index).Pixels.Span;
                for (var pixel = 3; pixel < expected.Length; pixel += 4) if (expected[pixel] != actual[pixel]) return false;
                return true;
            }
        }
        finally
        {
            preferences.Theme = originalTheme;
            preferences.PetId = originalPet;
            preferences.PetEnabled = originalEnabled;
            PackageLibrary.Shared.Remove(PackageKind.Theme, "community.meu-tema");
            foreach (var id in new[] { themeId, "community.meu-mascote", petId })
            {
                PackageLibrary.Shared.Remove(id == themeId ? PackageKind.Theme : PackageKind.Mascot, id);
            }
        }
    }
}
