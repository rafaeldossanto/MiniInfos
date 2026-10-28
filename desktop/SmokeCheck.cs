using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MiniInfos.Providers;
using MiniInfos.Settings;

namespace MiniInfos.Desktop;

/// <summary>Explicit native CI mode, with synthetic data and an isolated data directory.</summary>
internal static class SmokeCheck
{
    public static bool Enabled => Environment.GetCommandLineArgs().Contains("--smoke-ui");
    public static IReadOnlyList<IUsageProvider> Providers => new[] { "claude", "codex", "gemini", "kimi" }.Select(id => (IUsageProvider)new Sample(id)).ToArray();
    public static void Start(DesktopSession session, IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var stage = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        timer.Tick += (_, _) =>
        {
            try
            {
                switch (stage++)
                {
                    case 0: session.Preferences.FollowsApps = false; session.Preferences.Theme = ThemeChoice.Classic; break;
                    case 1: session.OpenSettings(); session.Preferences.Language = LanguageChoice.English; break;
                    case 2: session.Preferences.Theme = ThemeChoice.ClassicDark; session.Preferences.PetEnabled = true; break;
                    case 3: session.Preferences.Theme = ThemeChoice.ClassicLight; ((UI.CardWindow)lifetime.MainWindow!).ToggleSplit(); break;
                    case 4: session.Preferences.TextFont = FontChoice.Cinzel; session.Preferences.NumberFont = FontChoice.Orbitron; break;
                    case 5: session.Preferences.Language = LanguageChoice.Portuguese; break;
                    case 6:
                        lifetime.MainWindow!.Close();
                        if (lifetime.MainWindow.IsVisible) throw new InvalidOperationException("Closing the main card should hide it.");
                        session.ShowCards();
                        if (!lifetime.MainWindow.IsVisible) throw new InvalidOperationException("The hidden main card did not reopen.");
                        break;
                    default: timer.Stop(); Console.WriteLine("MiniInfos native UI smoke: passed (cards, split, settings, themes, fonts, languages, pet)."); session.Quit(); break;
                }
            }
            catch (Exception exception) { timer.Stop(); Console.Error.WriteLine(exception); session.Dispose(); lifetime.Shutdown(1); }
        };
        timer.Start();
    }
    private sealed class Sample(string id) : IUsageProvider
    {
        public string Id => id;
        public bool IsInstalled() => true;
        public bool IsInUse() => true;
        public void Flush() { }
        public Task<UsagePage> ReadAsync(DateTimeOffset now, bool isShown, bool force) => Task.FromResult(new UsagePage(id,
            new WindowLine(new PlanWindow(TimeSpan.FromHours(5), new RateLimit("five_hour", 42, now.AddHours(2).ToString("O"))), new TokenCount(31_200_000, 0, 0, 0)),
            new WindowLine(new PlanWindow(TimeSpan.FromDays(7), new RateLimit("weekly", 68, now.AddDays(3).ToString("O"))), new TokenCount(184_000_000, 0, 0, 0)),
            new TokenCount(12_800_000, 0, 0, 0), new TokenCount(642_000_000, 0, 0, 0), "CI fixture", "MiniInfos · 62%", "CI fixture", L.Text("agora"), L.Text("Atualizar agora")));
    }
}
