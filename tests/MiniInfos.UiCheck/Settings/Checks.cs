namespace MiniInfos.UiCheck;

internal static partial class Program
{
    /// <summary>The Mascote page, the longest: a thin bar in its margin, its rows as wide as a page that doesn't scroll.</summary>
    private static void CheckLongPageScrolls(TabControl pages, double rowsWidth)
    {
        pages.SelectedIndex = 2;
        Pump(TimeSpan.FromMilliseconds(300));
        var scroll = (ScrollViewer)((TabItem)pages.Items[2]).Content;
        scroll.ScrollToEnd();
        Pump(TimeSpan.FromMilliseconds(300));
        var bar = (ScrollBar)scroll.Template.FindName("PART_VerticalScrollBar", scroll);
        var thumb = (Border)bar.Track.Thumb.Template.FindName("Bar", bar.Track.Thumb);
        Check(bar.IsVisible && scroll.VerticalOffset > 0 && thumb.ActualWidth <= 6,
            $"a page taller than the window scrolls with a thin bar ({thumb.ActualWidth:0} px, scrolled {scroll.VerticalOffset:0})");
        var width = ((FrameworkElement)scroll.Content).ActualWidth;
        Check(Math.Abs(width - rowsWidth) < 1, $"and its rows keep the width of the other pages ({width:0} = {rowsWidth:0})");
        scroll.ScrollToHome();
        pages.SelectedIndex = 0;
        Pump(TimeSpan.FromMilliseconds(300));
    }

    private static void CheckSettings(CardSet cards, Preferences preferences, string pictures)
    {
        var card = cards.Main;
        var face = Find<Border>(card, "Card");
        var gear = Find<Button>(card, "SettingsButton");
        var at = gear.TranslatePoint(new Point(0, 0), face);
        Check(at.X < 24 && at.Y + gear.ActualHeight > face.ActualHeight - 16,
            $"the gear sits in the card's bottom-left corner ({at.X:0},{at.Y:0} in {face.ActualWidth:0}x{face.ActualHeight:0})");

        gear.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        PumpUntil(() => cards.Settings is { IsVisible: true }, TimeSpan.FromSeconds(5));
        var settings = cards.Settings;
        Check(settings is { IsVisible: true } && settings.Card == card, "the gear opens Configurações for its card");
        if (settings is null) return;

        Pump(TimeSpan.FromMilliseconds(500));
        gear.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(TimeSpan.FromMilliseconds(300));
        Check(cards.Settings == settings, "pressed again, the same window comes forward");

        var pages = Find<TabControl>(settings, "Pages");
        var names = pages.Items.OfType<TabItem>().Select(page => page.Header as string).ToList();
        Check(names.SequenceEqual(["Geral", "Aparência", "Mascote", "Sobre"]), $"with its pages ({string.Join(", ", names)})");
        var rowsWidth = ((FrameworkElement)((TabItem)pages.Items[0]).Content).ActualWidth;
        RenderPages(settings, pages, pictures, "dark");
        CheckLongPageScrolls(pages, rowsWidth);

        var topmost = Find<CheckBox>(settings, "TopmostToggle");
        topmost.IsChecked = false;
        Pump(TimeSpan.FromMilliseconds(300));
        Check(!preferences.IsTopmost && !card.Topmost && !Find<MenuItem>(card, "TopmostItem").IsChecked && SharedSettings().Contains("\"IsTopmost\":false"),
            "Sempre por cima off lets the card fall behind, unticks its menu item and is kept");
        topmost.IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(300));
        Check(card.Topmost && Find<MenuItem>(card, "TopmostItem").IsChecked, "back on, it stays in front again");

        var follow = Find<CheckBox>(settings, "FollowToggle");
        follow.IsChecked = false;
        Check(!preferences.FollowsApps && SharedSettings().Contains("\"FollowsApps\":false"), "Acompanhar o Claude e o Codex off is kept");
        follow.IsChecked = true;

        var shortcut = System.IO.Path.Combine(Environment.GetEnvironmentVariable("MINIINFOS_STARTUP_DIR")!, "MiniInfos.lnk");
        var startup = Find<CheckBox>(settings, "StartupToggle");
        startup.IsChecked = true;
        Check(File.Exists(shortcut) && startup.IsChecked == true, "Iniciar com o Windows makes the startup shortcut");
        startup.IsChecked = false;
        Check(!File.Exists(shortcut), "and off, takes it away");

        Find<RadioButton>(settings, "DetailedNumbers").IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(300));
        Check(card.IsDetailed && Find<RadioButton>(card, "DetailedView").IsChecked == true, "Números: Detalhado flips the card's own switch");
        Find<RadioButton>(card, "CompactView").IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(300));
        Check(Find<RadioButton>(settings, "CompactNumbers").IsChecked == true, "and the card's switch shows in the settings");

        Find<Button>(settings, "CloseButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(TimeSpan.FromMilliseconds(300));
        Check(cards.Settings is null && !settings.IsVisible, "its close button closes it");
    }
}
