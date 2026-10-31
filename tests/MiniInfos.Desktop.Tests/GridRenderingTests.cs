using Avalonia;
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

public sealed class GridRenderingTests
{
    [AvaloniaFact]
    public async Task AGridRestoresAllPagesCanAddAndRemoveAndKeepsTheFooterVisible()
    {
        using var folder = new TempFolder();
        var preferences = new Preferences(folder.File("settings.json")) { Theme = ThemeChoice.ClassicDark };
        IUsageProvider[] providers = Enumerable.Range(0, 8).Select(index => (IUsageProvider)new Fixture("provider-" + index)).ToArray();
        var session = new MiniInfos.Desktop.DesktopSession(null!, preferences, providers);
        foreach (var provider in session.Providers) session.Pages[provider.Id] = await provider.ReadAsync(DateTimeOffset.Now, true, false);
        var store = CardStore.NewExtra(new CardSettings(50, 50, 1.5, false, providers[0].Id, providers[1].Id, providers.Select(provider => provider.Id).ToArray()));
        var card = new CardWindow(session, store);
        try
        {
            card.Show();
            using var frame = card.CaptureRenderedFrame();
            Assert.NotNull(frame);
            Assert.All(providers, provider => Assert.True(card.Shows(provider.Id)));
            Assert.Equal(8, card.GetVisualDescendants().OfType<TextBlock>().Count(text => text.Text?.StartsWith("Fixture provider-") == true));
            Assert.True(card.GetVisualDescendants().OfType<ScrollViewer>().Any());
            var scroller = card.GetVisualDescendants().OfType<ScrollViewer>().First();
            scroller.Offset = new Vector(0, 80);
            var priorOffset = scroller.Offset.Y;
            var firstPage = session.Pages[providers[0].Id];
            session.Pages[providers[0].Id] = firstPage with { Today = new TokenCount(100, 0, 0, 0) };
            card.Render();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            using var updatedFrame = card.CaptureRenderedFrame();
            Assert.True(priorOffset > 0);
            Assert.Equal(priorOffset, card.GetVisualDescendants().OfType<ScrollViewer>().First().Offset.Y);
            foreach (var button in card.GetVisualDescendants().OfType<Button>())
            {
                var point = button.TranslatePoint(new Point(), card)!.Value;
                Assert.True(point.Y + button.Bounds.Height <= card.ClientSize.Height + 1,
                    $"{button.Name} {button.Content}: bottom={point.Y + button.Bounds.Height}, card={card.ClientSize.Height}, visible={button.IsVisible}, effective={button.IsEffectivelyVisible}");
            }
            card.SetSelection(PageSelection.Restore(Enumerable.Range(0, 8), 8).Remove(3));
            Assert.False(card.Shows(providers[3].Id));
            Assert.True(card.Shows(providers[7].Id));
            Assert.Equal(7, store.Load()!.SelectedPages.Count);
            card.SetSelection(PageSelection.Restore(Enumerable.Range(0, 8).Where(index => index != 3), 8).SplitWith(3));
            Assert.True(card.Shows(providers[3].Id));
            Assert.Equal(8, store.Load()!.SelectedPages.Count);
        }
        finally { card.Close(); store.Forget(); }
    }

    private sealed class Fixture(string id) : IUsageProvider
    {
        public string Id => id;
        public bool IsInstalled() => true;
        public bool IsInUse() => false;
        public void Flush() { }
        public Task<UsagePage> ReadAsync(DateTimeOffset now, bool isShown, bool force) => Task.FromResult(new UsagePage("Fixture " + id,
            new(new PlanWindow(TimeSpan.FromHours(5), null), default), new(new PlanWindow(TimeSpan.FromDays(7), null), default),
            default, default, "Fixture", "Fixture", "Fixture", "agora", "Fixture"));
    }
}
