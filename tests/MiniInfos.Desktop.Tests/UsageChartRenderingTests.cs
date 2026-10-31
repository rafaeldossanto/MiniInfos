using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MiniInfos.Desktop.UI;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Providers;
using MiniInfos.Settings;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Desktop;

public sealed class UsageChartRenderingTests
{
    [AvaloniaFact]
    public async Task ChartsFillAShorterPageAndTheSavedOptionAlsoShowsThemOnSingleCards()
    {
        foreach (var theme in new[] { ThemeChoice.Simple, ThemeChoice.Light }) await CheckCharts(theme);
    }

    private static async Task CheckCharts(ThemeChoice theme)
    {
        using var folder = new TempFolder();
        var preferences = new Preferences(folder.File("settings.json")) { Theme = theme };
        IUsageProvider[] providers = [new Fixture("codex"), new Fixture("antigravity")];
        var session = new MiniInfos.Desktop.DesktopSession(null!, preferences, providers);
        foreach (var provider in providers) session.Pages[provider.Id] = await provider.ReadAsync(DateTimeOffset.Now, true, false);
        var store = CardStore.NewExtra(new CardSettings(50, 50, 1, false, "codex", "antigravity"));
        var card = new CardWindow(session, store);
        try
        {
            card.Show();
            using var frame = card.CaptureRenderedFrame();
            Assert.NotNull(frame);
            Assert.Single(card.GetVisualDescendants().OfType<UsageChart>());
            var automaticHeight = card.Height;
            preferences.AlwaysShowUsageChart = true; card.Render();
            Assert.Equal(2, card.GetVisualDescendants().OfType<UsageChart>().Count());
            preferences.AlwaysShowUsageChart = false; card.Render();
            Assert.InRange(Math.Abs(automaticHeight - card.Height), 0, 1);
            card.SetSelection(PageSelection.Single(0, 2));
            Assert.Empty(card.GetVisualDescendants().OfType<UsageChart>());
            preferences.AlwaysShowUsageChart = true; card.Render();
            Assert.Single(card.GetVisualDescendants().OfType<UsageChart>());
            using var single = card.CaptureRenderedFrame();
            Assert.NotNull(single);
            Assert.Single(card.GetVisualDescendants().OfType<UsageChart>());
        }
        finally { card.Close(); store.Forget(); }
    }

    private sealed class Fixture(string id) : IUsageProvider
    {
        public string Id => id;
        public bool IsInstalled() => true;
        public bool IsInUse() => false;
        public void Flush() { }
        public Task<UsagePage> ReadAsync(DateTimeOffset now, bool isShown, bool force)
        {
            var five = new PlanWindow(TimeSpan.FromHours(5), null);
            var week = new PlanWindow(TimeSpan.FromDays(7), null);
            return Task.FromResult(new UsagePage(id, new(five, default), new(week, default), default, default,
                "Fixture", "Fixture", "Fixture", "agora", "Fixture",
                id == "antigravity" ? Enumerable.Range(0, 3).Select(index => new UsageQuotaGroup("Group " + index, "Models", [five, week])).ToArray() : null,
                new(now, Enumerable.Range(1, 18).Select(value => (long)value).ToArray())));
        }
    }
}
