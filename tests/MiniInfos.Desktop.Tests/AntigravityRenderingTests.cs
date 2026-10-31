using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MiniInfos.Desktop.UI;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Providers.Antigravity;
using MiniInfos.Settings;
using MiniInfos.Tests.Providers.Antigravity;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Desktop;

public sealed class AntigravityRenderingTests
{
    [AvaloniaFact]
    public async Task ModelGroupsAndFooterFitInsideTheNativeSplitCard()
    {
        using var folder = new TempFolder();
        var preferences = new Preferences(folder.File("settings.json")) { Theme = ThemeChoice.Classic };
        var anti = new AntigravityUsage(() => [], folder.File("ledger.json"), _ => Task.FromResult<string?>(AntigravityQuotaTests.Summary()));
        var codex = MiniInfos.Desktop.SmokeCheck.Providers.Single(provider => provider.Id == "codex");
        var session = new MiniInfos.Desktop.DesktopSession(null!, preferences, [codex, anti]);
        foreach (var provider in session.Providers) session.Pages[provider.Id] = await provider.ReadAsync(DateTimeOffset.Now, true, false);
        var store = CardStore.NewExtra(new CardSettings(50, 50, 1, false, "codex", "antigravity"));
        var card = new CardWindow(session, store);
        try
        {
            card.Show();
            using var frame = card.CaptureRenderedFrame(); Assert.NotNull(frame);
            var visuals = card.GetVisualDescendants().ToArray();
            Assert.Contains(visuals.OfType<TextBlock>(), text => text.Text == "Gemini 4 Argon");
            Assert.Contains(visuals.OfType<TextBlock>(), text => text.Text == "Gemini Pro · Gemini Flash");
            Assert.Equal(5, visuals.OfType<ProgressBar>().Count(bar => bar.Height == 3));
            Assert.Contains(visuals, item => item is PathIcon);
            foreach (var button in visuals.OfType<Button>())
            {
                var point = button.TranslatePoint(new Point(), card)!.Value;
                Assert.True(point.Y + button.Bounds.Height <= card.ClientSize.Height + 1, "Grouped quotas clipped the footer.");
            }
        }
        finally { card.Close(); store.Forget(); }
    }
}
