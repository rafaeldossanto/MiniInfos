namespace MiniInfos.UiCheck;

internal static partial class Program
{
    private static void CheckAntigravity(Preferences preferences, string pictures)
    {
        var priorTheme = preferences.Theme;
        preferences.Theme = ThemeChoice.Classic;
        var store = MiniInfos.Infrastructure.Storage.CardStore.NewExtra(new CardSettings(60, 60, 1, false, "antigravity"));
        var window = new MainWindow([new SampleProvider("codex", "Codex (GPT)"), new GroupedSample()], preferences, store, CardKind.Extra);
        try
        {
            window.Show();
            PumpUntil(() => window.Opacity == 1 && Find<QuotaGroupsView>(Columns(window)[0], "GroupedWindows").Children.Count > 0, TimeSpan.FromSeconds(8));
            var column = Columns(window)[0];
            var groups = Find<QuotaGroupsView>(column, "GroupedWindows");
            var visuals = QuotaVisuals(groups).ToArray();
            var text = visuals.OfType<TextBlock>().Select(block => new TextRange(block.ContentStart, block.ContentEnd).Text.TrimEnd('\r', '\n')).ToArray();
            var bars = visuals.OfType<ProgressBar>().ToArray();
            Check(text.Contains("Gemini 4 Argon") && text.Contains("Gemini") && text.Contains("Claude e GPT"), "all three independent model groups are visible");
            Check(bars.Length == 5 && bars.All(bar => bar.Height == 3), "five thin meters, with only one weekly meter for Argon");
            Check(Math.Abs(bars[0].Value - 42) < .01 && bars.Skip(1).All(bar => bar.Value == 0), "42 percent used fills Argon; fully available groups remain empty");
            Check(text.Contains("Semanal") && text.Any(value => value.StartsWith("58% restante")), "Argon keeps its weekly label and remaining/reset caption");
            Check(!text.Any(value => value.Contains("High") || value.Contains("Tokens somados")), "the two removed captions are absent");
            Check(Find<StackPanel>(column, "StandardWindows").Visibility == Visibility.Collapsed && Find<TextBlock>(column, "ChatText").Visibility == Visibility.Collapsed,
                "the standard two-window layout and bottom activity do not leave extra rows");
            Check(bars[1].TranslatePoint(new Point(), groups).Y == bars[2].TranslatePoint(new Point(), groups).Y, "5h and weekly share a row inside each model group");
            Check(window.Height < 520, $"the classic single card remains compact ({window.Height:0} px)");
            Render(window, System.IO.Path.Combine(pictures, "antigravity-single.png"));

            var icons = Find<StackPanel>(window, "PageIcons");
            ((Button)icons.Children[0]).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            PumpUntil(() => Find<TextBlock>(Columns(window)[0], "PageTitle").Text == "Codex (GPT)", TimeSpan.FromSeconds(3));
            var (_, split, _, _) = OpenPageMenu(icons, 1); Choose(split);
            Check(Columns(window).Count == 2 && Find<TextBlock>(Columns(window)[0], "PageTitle").Text == "Codex (GPT)"
                && Find<TextBlock>(Columns(window)[1], "PageTitle").Text == "Antigravity", "Codex and Antigravity render together in split mode");
            var right = Columns(window)[1];
            Check(Find<QuotaGroupsView>(right, "GroupedWindows").ActualWidth <= right.ActualWidth + 1, "group meters fit inside their split column");
            Check(window.Height < 520, "split mode preserves the compact height");
            var official = (Geometry)((System.Windows.Shapes.Path)((Button)icons.Children[1]).Content).Data;
            Check(official.Bounds.Width > 70 && official.Bounds.Width < 100, "the footer uses the official curved silhouette, without a letter badge");
            Render(window, System.IO.Path.Combine(pictures, "antigravity-split.png"));
            var dark = ((SolidColorBrush)QuotaVisuals(Find<QuotaGroupsView>(right, "GroupedWindows")).OfType<ProgressBar>().First().Foreground).Color;
            preferences.Theme = ThemeChoice.ClassicLight; Pump(TimeSpan.FromMilliseconds(300));
            var light = ((SolidColorBrush)QuotaVisuals(Find<QuotaGroupsView>(right, "GroupedWindows")).OfType<ProgressBar>().First().Foreground).Color;
            Check(light != dark, "group meters follow a live switch to the light theme");
            Render(window, System.IO.Path.Combine(pictures, "antigravity-split-light.png"));
        }
        finally { window.PrepareToQuit(); window.Close(); store.Forget(); preferences.Theme = priorTheme; }
    }

    private static IEnumerable<DependencyObject> QuotaVisuals(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index); yield return child;
            foreach (var item in QuotaVisuals(child)) yield return item;
        }
    }

    private sealed class GroupedSample : IUsageProvider
    {
        public string Id => "antigravity";
        public bool IsInstalled() => true;
        public bool IsInUse() => true;
        public void Flush() { }
        public Task<UsagePage> ReadAsync(DateTimeOffset now, bool isShown, bool force)
        {
            PlanWindow Window(int hours, double percent) => new(TimeSpan.FromHours(hours), new RateLimit("fixture", percent, now.AddHours(hours).ToString("O")));
            var week = Window(168, 42);
            return Task.FromResult(new UsagePage("Antigravity", new WindowLine(Window(5, 0), default), new WindowLine(week, default),
                new TokenCount(10_600_000, 0, 0, 0), new TokenCount(10_600_000, 0, 0, 0), "Fixture", "", "Fixture", "atualizado agora", "Fixture",
                [new("Gemini 4 Argon", null, [week]), new("Gemini", "Gemini Pro · Gemini Flash", [Window(5, 0), Window(168, 0)]),
                    new("Claude e GPT", "Opus · Sonnet · GPT OSS", [Window(5, 0), Window(168, 0)])]));
        }
    }
}
