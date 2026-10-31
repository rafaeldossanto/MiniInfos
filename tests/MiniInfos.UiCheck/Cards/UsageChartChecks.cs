namespace MiniInfos.UiCheck;

internal static partial class Program
{
    private static void CheckUsageCharts(Preferences preferences, string pictures)
    {
        var priorTheme = preferences.Theme;
        var priorChart = preferences.AlwaysShowUsageChart;
        preferences.AlwaysShowUsageChart = false;
        preferences.Theme = ThemeChoice.Simple;
        var store = MiniInfos.Infrastructure.Storage.CardStore.NewExtra(new CardSettings(60, 60, 1, false, "codex", "antigravity"));
        var window = new MainWindow([new ChartSample(), new GroupedSample()], preferences, store, CardKind.Extra);
        try
        {
            window.Show();
            PumpUntil(() => window.Opacity == 1 && Find<StackPanel>(Columns(window)[0], "Numbers").Visibility == Visibility.Visible, TimeSpan.FromSeconds(8));
            Pump(TimeSpan.FromMilliseconds(200));
            var shortPage = Columns(window)[0]; var tallPage = Columns(window)[1];
            var chart = Find<UsageChart>(shortPage, "RecentUsage");
            Check(chart.IsVisible && !Find<UsageChart>(tallPage, "RecentUsage").IsVisible,
                $"the shorter page automatically fills spare space with its chart (base heights {shortPage.NaturalHeight:0}/{tallPage.NaturalHeight:0})");
            var height = window.Height;
            Render(window, System.IO.Path.Combine(pictures, "chart-automatic-dark.png"));
            preferences.AlwaysShowUsageChart = true; Pump(TimeSpan.FromMilliseconds(200));
            Check(Columns(window).All(page => Find<UsageChart>(page, "RecentUsage").IsVisible), "always showing includes the taller page without displacing its quota groups");
            preferences.AlwaysShowUsageChart = false; Pump(TimeSpan.FromMilliseconds(200));
            Check(Math.Abs(window.Height - height) <= 1, $"turning always off restores the automatic split height ({height:0.0} → {window.Height:0.0})");
            var icons = Find<StackPanel>(window, "PageIcons");
            ((Button)icons.Children[0]).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); Pump(TimeSpan.FromMilliseconds(300));
            Check(!Find<UsageChart>(Columns(window)[0], "RecentUsage").IsVisible, "a single card stays compact in automatic mode");
            preferences.AlwaysShowUsageChart = true; Pump(TimeSpan.FromMilliseconds(200));
            Check(Find<UsageChart>(Columns(window)[0], "RecentUsage").IsVisible, "always shows a single card's chart immediately");
            Render(window, System.IO.Path.Combine(pictures, "chart-single-dark.png"));
            preferences.Theme = ThemeChoice.Light; Pump(TimeSpan.FromMilliseconds(300));
            Render(window, System.IO.Path.Combine(pictures, "chart-single-light.png"));
            Check(Find<UsageChart>(Columns(window)[0], "RecentUsage").ActualHeight == UsageChartLayout.Height, "the chart stays within its reserved height in the light theme");
        }
        finally { window.PrepareToQuit(); window.Close(); store.Forget(); preferences.AlwaysShowUsageChart = priorChart; preferences.Theme = priorTheme; }
    }

    private sealed class ChartSample : IUsageProvider
    {
        private readonly SampleProvider _sample = new("codex", "Codex (GPT)");
        public string Id => "codex";
        public bool IsInstalled() => true;
        public bool IsInUse() => false;
        public void Flush() { }
        public async Task<UsagePage> ReadAsync(DateTimeOffset now, bool isShown, bool force) =>
            (await _sample.ReadAsync(now, isShown, force)) with { History = new(now, [5, 3, 9, 6, 12, 6, 10, 8, 16, 22, 9, 13, 6, 20, 29, 17, 25, 22]) };
    }
}
