namespace MiniInfos.UiCheck;

internal static partial class Program
{
    private static void CheckMultiPage(Preferences preferences, string pictures)
    {
        var priorTheme = preferences.Theme;
        preferences.Theme = ThemeChoice.ClassicDark;
        IUsageProvider[] providers = [new SampleProvider("claude", "Claude"), new SampleProvider("codex", "Codex (GPT)"),
            new SampleProvider("gemini", "Gemini", false), new SampleProvider("kimi", "Kimi", false), new GroupedSample()];
        var store = MiniInfos.Infrastructure.Storage.CardStore.NewExtra(new CardSettings(40, 40, 1, false, "claude", "codex"));
        var window = new MainWindow(providers, preferences, store, CardKind.Extra);
        try
        {
            window.Show();
            PumpUntil(() => window.Opacity == 1 && Find<TextBlock>(Columns(window)[0], "Reading").Visibility == Visibility.Collapsed, TimeSpan.FromSeconds(8));
            var icons = Find<StackPanel>(window, "PageIcons");
            for (var index = 2; index < providers.Length; index++)
            {
                var (_, add, _, _) = OpenPageMenu(icons, index);
                Check(Offered(add) && add.Header as string == "Adicionar à visualização", "a split view offers adding the next provider");
                Choose(add);
                if (index == 3) Render(window, System.IO.Path.Combine(pictures, "four-page-grid.png"));
            }
            Check(Columns(window).Count == 5, "all five providers stay in the same card");
            Check(Enumerable.Range(0, 5).All(index => IconIsLit(icons, index)), "every selected provider is lit in the centered footer");
            Check(Columns(window).Select(Grid.GetRow).Distinct().Count() > 1, "pages wrap into a grid when the screen is too narrow");
            Check(window.Width <= SystemParameters.WorkArea.Width + 1 && window.Height <= SystemParameters.WorkArea.Height + 1,
                "the grid stays within the available screen area");
            Check(store.Load()!.SelectedPages.SequenceEqual(providers.Select(provider => provider.Id)), "every selected page is persisted in order");
            Check(Find<ScrollViewer>(window, "PagesScroll").ExtentHeight > 0, "the page area has a measurable scroll extent");
            Render(window, System.IO.Path.Combine(pictures, "multi-page-grid.png"));

            preferences.Theme = ThemeChoice.ClassicLight;
            Pump(TimeSpan.FromMilliseconds(400));
            Check(Columns(window).Count == 5 && Columns(window).All(page => page.ActualWidth >= 262), "switching theme keeps all five complete pages");
            Render(window, System.IO.Path.Combine(pictures, "multi-page-grid-light.png"));

            store.Save(store.Load()! with { Scale = 3 });
            var restored = new MainWindow(providers, preferences, store, CardKind.Extra);
            try
            {
                restored.Show();
                PumpUntil(() => restored.Opacity == 1, TimeSpan.FromSeconds(8));
                Check(restored.ShownPages.SequenceEqual([0, 1, 2, 3, 4]), "reopening restores every page of the grid");
                Check(restored.Height <= SystemParameters.WorkArea.Height + 1 && restored.Width <= SystemParameters.WorkArea.Width + 1,
                    "a restored grid at maximum saved scale fits the available screen");
                var scroll = Find<ScrollViewer>(restored, "PagesScroll");
                scroll.ScrollToBottom(); Pump(TimeSpan.FromMilliseconds(100));
                Check(scroll.ScrollableHeight > 0 && scroll.VerticalOffset > 0, "a tall grid scrolls down to the remaining pages");
                var priorOffset = scroll.VerticalOffset;
                restored.ForceRefreshAsync(); Pump(TimeSpan.FromSeconds(1));
                Check(Math.Abs(scroll.VerticalOffset - priorOffset) < 1, "refreshing keeps the scrolled page area in place");
            }
            finally { restored.PrepareToQuit(); restored.Close(); }

            var button = (Button)icons.Children[2];
            button.ContextMenu!.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
            Pump(TimeSpan.FromMilliseconds(100));
            var open = (MenuItem)button.ContextMenu.Items[0];
            Choose(open.Items.OfType<MenuItem>().Single(item => item.Header as string == "Remover desta visualização"));
            Check(Columns(window).Count == 4 && !window.ShownPages.Contains(2), "removing a page preserves the other four");

            window.ShowProviders([providers[0], providers[3], providers[4]]);
            Pump(TimeSpan.FromMilliseconds(500));
            Check(Columns(window).Count == 3 && window.ShownPages.SequenceEqual([0, 1, 2]), "uninstalled providers are removed without discarding the other selected pages");
        }
        finally { window.PrepareToQuit(); window.Close(); store.Forget(); preferences.Theme = priorTheme; }
    }
}
