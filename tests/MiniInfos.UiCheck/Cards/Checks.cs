namespace MiniInfos.UiCheck;

internal static partial class Program
{
    /// <summary>Popups have their own visuals: capture them as well as the card for theme review.</summary>
    private static void RenderMenus(MainWindow window, string pictures)
    {
        var card = Find<Border>(window, "Card");
        var menu = card.ContextMenu!;
        menu.PlacementTarget = card;
        menu.IsOpen = true;
        Pump(TimeSpan.FromMilliseconds(300));
        RenderElement(menu, System.IO.Path.Combine(pictures, "context-menu.png"));
        menu.IsOpen = false;

        var (openIn, _, _, _) = OpenPageMenu(Find<StackPanel>(window, "PageIcons"), 1);
        openIn.IsSubmenuOpen = true;
        Pump(TimeSpan.FromMilliseconds(300));
        RenderElement((ContextMenu)openIn.Parent, System.IO.Path.Combine(pictures, "icon-menu.png"));
        var popup = (Popup)openIn.Template.FindName("PART_Popup", openIn);
        RenderElement((FrameworkElement)popup.Child, System.IO.Path.Combine(pictures, "icon-submenu.png"));
        openIn.IsSubmenuOpen = false;
        ((ContextMenu)openIn.Parent).IsOpen = false;
    }

    private static void CheckSplit(MainWindow window, IntPtr handle, string pictures)
    {
        var icons = Find<StackPanel>(window, "PageIcons");
        var singleWidth = window.Width;

        var (openIn, split, single, _) = OpenPageMenu(icons, 1);
        Check(openIn.Header as string == "Abrir em" && Offered(split) && !Offered(single),
            "right-clicking a page icon offers Abrir em > Visualização dividida");
        Choose(split);

        Check(Columns(window).Count == 2, "Visualização dividida shows two pages side by side");
        Check(IconIsLit(icons, 0) && IconIsLit(icons, 1), "and lights both their icons");
        Check(Find<TextBlock>(Columns(window)[0], "PageTitle").Text != Find<TextBlock>(Columns(window)[1], "PageTitle").Text,
            $"each column shows its own provider ({Find<TextBlock>(Columns(window)[0], "PageTitle").Text} | {Find<TextBlock>(Columns(window)[1], "PageTitle").Text})");
        Check(Find<TextBlock>(Columns(window)[0], "Note").Text != "" && Find<TextBlock>(Columns(window)[1], "Note").Text != "",
            "each column says how fresh its own limits are");
        Check(window.Width > singleWidth * 1.8 && window.Width < singleWidth * 2.1, $"the card widens to hold them ({singleWidth:0} -> {window.Width:0})");
        Check(window.Left >= SystemParameters.VirtualScreenLeft
            && window.Left + window.Width <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth, "and stays on screen");
        Check(Settings().Contains("\"BesidePage\":\"codex\""), "the split is kept for next time");
        CheckDragKeepsProportions(window, handle, "split");
        Render(window, System.IO.Path.Combine(pictures, "split.png"));

        var (_, splitAgain, back, _) = OpenPageMenu(icons, 0);
        Check(Offered(back) && !Offered(splitAgain), "a split card offers Visualização única");
        Choose(back);
        Check(Columns(window).Count == 1 && IconIsLit(icons, 0) && !IconIsLit(icons, 1), "Visualização única goes back to one page");
        Check(Math.Abs(window.Width - singleWidth) < 2, $"at the card's own width ({window.Width:0})");
    }

    private static void CheckNewWindow(CardSet cards, string pictures)
    {
        var (_, _, _, newWindow) = OpenPageMenu(Find<StackPanel>(cards.Main, "PageIcons"), 1);
        Choose(newWindow);
        PumpUntil(() => cards.Extras.Count == 1 && cards.Extras[0].Opacity == 1, TimeSpan.FromSeconds(10));

        var extra = cards.Extras.FirstOrDefault();
        Check(extra is { IsVisible: true }, "Nova janela opens a card of its own");
        if (extra is null) return;

        Pump(TimeSpan.FromSeconds(1));
        Check(Find<TextBlock>(Columns(extra)[0], "PageTitle").Text == "Codex (GPT)", "showing the page it was opened from");
        Check(Directory.EnumerateFiles(System.IO.Path.Combine(Data, "windows"), "*.json").Count() == 1, "and kept, so it comes back with the app");
        Check((Find<Button>(extra, "DismissButton").ToolTip as string) == "Fechar esta janela", "its ✕ closes it");
        var mainIcons = Find<StackPanel>(cards.Main, "PageIcons");
        var extraIcons = Find<StackPanel>(extra, "PageIcons");
        Check(IconIsLit(mainIcons, 0) && IconIsLit(mainIcons, 1) && IconIsLit(extraIcons, 0) && IconIsLit(extraIcons, 1),
            "with Claude in one card and Codex in the other, both pagers light both icons");
        Render(extra, System.IO.Path.Combine(pictures, "new-window.png"));

        ((Button)extraIcons.Children[0]).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(TimeSpan.FromSeconds(1));
        Check(!IconIsLit(mainIcons, 1) && IconIsLit(extraIcons, 0) && !IconIsLit(extraIcons, 1),
            "when both cards show Claude, Codex's icon goes dark in both");
        ((Button)extraIcons.Children[1]).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(TimeSpan.FromSeconds(1));

        cards.Hide();
        Check(!extra.IsVisible && !cards.Main.IsVisible, "hiding the cards hides it too");
        cards.BringBack();
        Pump(TimeSpan.FromSeconds(1));
        Check(extra.IsVisible && cards.Main.IsVisible, "and bringing them back brings it back");

        Find<Button>(extra, "DismissButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(TimeSpan.FromSeconds(1));
        Check(cards.Extras.Count == 0 && !Directory.EnumerateFiles(System.IO.Path.Combine(Data, "windows"), "*.json").Any(),
            "closing it forgets it for good");
        Check(IconIsLit(mainIcons, 0) && !IconIsLit(mainIcons, 1), "and its page's icon goes dark in the main card");
    }

    private static void CheckEdges(IntPtr handle)
    {
        var rect = WindowRect(handle);
        int Hit(int x, int y) => (int)SendMessage(handle, WmNcHitTest, IntPtr.Zero, (y << 16) | (x & 0xFFFF));
        Check(Hit(rect.Right - 3, rect.Bottom - 3) == HtBottomRight, "the bottom-right corner resizes");
        Check(Hit(rect.Left + 2, rect.Top + rect.Height / 2) == HtLeft, "the left edge resizes");
        Check(Hit(rect.Left + rect.Width / 2, rect.Bottom - 2) == HtBottom, "the bottom edge resizes");
        Check(Hit(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2) == HtClient, "the middle is the card, to drag it in any direction");
    }

    private static void CheckTray(TrayIcon tray, List<string> summaries, MainWindow window)
    {
        var icon = (System.Windows.Forms.NotifyIcon)typeof(TrayIcon)
            .GetField("_icon", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(tray)!;
        Check(icon.Visible && icon.Icon is { Width: > 0 }, "the tray icon is up");
        Check(summaries.Count > 0 && icon.Text == summaries[^1], "hovering it reads the latest numbers");
        Check(Regex.IsMatch(icon.Text, @"Hoje [0-9]+(,[0-9])?[kMB]? tokens"), $"and their short form ({Line(icon.Text)})");
        Check(icon.ContextMenuStrip!.Items.Cast<System.Windows.Forms.ToolStripItem>().Any(item => item.Text == "Configurações"),
            "its menu opens Configurações");

        window.HideCard();
        var before = summaries.Count;
        Pump(TimeSpan.FromSeconds(4));
        Check(summaries.Count > before, "the numbers keep coming while the card is hidden");
        window.BringBack();
        Pump(TimeSpan.FromSeconds(1));
        Check(window.IsVisible, "Mostrar brings the card back");
    }

    private static void CheckViews(MainWindow window)
    {
        var total = Find<TextBlock>(Columns(window)[0], "TotalTokens");
        Find<RadioButton>(window, "CompactView").IsChecked = true;
        Pump(TimeSpan.FromSeconds(2));
        Check(Regex.IsMatch(total.Text, "^[0-9]+(,[0-9])?[kMB]?$"), $"Resumido shows the short form ({total.Text})");

        Find<RadioButton>(window, "DetailedView").IsChecked = true;
        Pump(TimeSpan.FromSeconds(2));
        Check(Regex.IsMatch(total.Text, @"^[0-9]{1,3}(\.[0-9]{3})*$"), $"Detalhado shows every digit ({total.Text})");
        Check(Settings().Contains("\"IsDetailed\":true"), "the view is kept for next time");
    }

    private static void CheckRefresh(MainWindow window, List<string> summaries)
    {
        var refresh = Find<Button>(window, "RefreshButton");
        var spin = Find<RotateTransform>(window, "RefreshSpin");
        var before = summaries.Count;
        refresh.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(TimeSpan.FromMilliseconds(250));
        Check(!refresh.IsEnabled && spin.HasAnimatedProperties, "refresh turns its icon while it reads");
        PumpUntil(() => refresh.IsEnabled, TimeSpan.FromSeconds(20));
        Check(summaries.Count > before && !spin.HasAnimatedProperties, "then it has read and stands still");
    }

    private static void CheckPages(MainWindow window, IntPtr handle, int pageCount, string pictures)
    {
        var title = Find<TextBlock>(Columns(window)[0], "PageTitle");
        var icons = Find<StackPanel>(window, "PageIcons");
        var first = title.Text;
        var height = window.ActualHeight;
        Check(icons.Children.Count == pageCount, $"one monochrome icon per provider ({icons.Children.Count})");
        Render(window, System.IO.Path.Combine(pictures, "page-1.png"));

        Find<Button>(window, "NextPageButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(TimeSpan.FromSeconds(1));
        var second = title.Text;
        Check(second != first, $"the next arrow turns the page ({first} -> {second})");
        Check(Settings().Contains("\"Page\":\"codex\""), "the page is kept for next time, by provider");

        // Asked for by hand on the page now shown: the limits come back from the provider itself.
        _ = window.ForceRefreshAsync();
        Pump(TimeSpan.FromMilliseconds(100));
        PumpUntil(() => Find<Button>(window, "RefreshButton").IsEnabled, TimeSpan.FromSeconds(20));
        if (!_quiet) Console.WriteLine($"     page 2: {Find<TextBlock>(Columns(window)[0], "ShortPercent").Text} / {Find<TextBlock>(Columns(window)[0], "LongPercent").Text} | {Find<TextBlock>(Columns(window)[0], "Note").Text} | {Find<TextBlock>(Columns(window)[0], "ChatText").Text}");
        Check(Math.Abs(window.ActualHeight - height) < 1, "both pages keep the card's size");
        Render(window, System.IO.Path.Combine(pictures, "page-2.png"));

        window.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(window)!, Environment.TickCount, System.Windows.Input.Key.Left)
        { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
        Pump(TimeSpan.FromSeconds(1));
        Check(title.Text == first, "the left arrow key turns it back");

        // One swipe: several sideways steps in a row, which must turn a single page.
        for (var step = 0; step < 4; step++) SendMessage(handle, WmMouseHorizontalWheel, (IntPtr)(120 << 16), 0);
        Pump(TimeSpan.FromSeconds(1));
        Check(title.Text == second, "a sideways touchpad swipe turns it, once per swipe");

        ((Button)icons.Children[0]).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(TimeSpan.FromSeconds(1));
        Check(title.Text == first, "a provider icon turns straight to its page");
    }

    private static void CheckInstalls(CardSet cards, string pictures)
    {
        var window = cards.Main;
        var icons = Find<StackPanel>(window, "PageIcons");
        var installed = cards.Providers;

        // However many pages there are, their icons sit in the middle of the card.
        foreach (var count in (int[])[1, 2, 3, 5])
        {
            window.ShowProviders([.. Enumerable.Range(0, count).Select(page => new SampleProvider(page == 0 ? "claude" : $"sample{page}", $"Teste {page + 1}"))]);
            Pump(TimeSpan.FromMilliseconds(300));
            var offset = CenterOffset(window, icons);
            Check(icons.Children.Count == count && Math.Abs(offset) < 1, $"{count} page icon(s) centered in the card ({offset:0.0} px off)");
            if (count == 5) Render(window, System.IO.Path.Combine(pictures, "five-pages.png"));
        }

        window.ShowProviders(installed);
        Pump(TimeSpan.FromMilliseconds(300));

        // The rest uninstalls apps: only the sample ones can be.
        if (installed is not [SampleProvider claude, SampleProvider codex]) return;

        var (_, split, _, _) = OpenPageMenu(icons, 1);
        Choose(split);
        var splitWidth = window.Width;
        codex.Installed = false;
        LookAgain(cards);
        Check(icons.Children.Count == 1 && Columns(window).Count == 1 && window.Width < splitWidth * 0.6,
            $"an app uninstalled takes its page away, and a card split with it goes back to one page ({splitWidth:0} -> {window.Width:0})");
        Check(Find<Button>(window, "PreviousPageButton").Visibility == Visibility.Hidden && Math.Abs(CenterOffset(window, icons)) < 1,
            "the page left keeps its icon, centered, with no arrows");
        Render(window, System.IO.Path.Combine(pictures, "one-page.png"));

        codex.Installed = true;
        LookAgain(cards);
        Check(icons.Children.Count == 2 && Find<TextBlock>(Columns(window)[0], "PageTitle").Text == "Claude", "installed again, its page comes back");

        claude.Installed = codex.Installed = false;
        LookAgain(cards);
        Check(icons.Children.Count == 2, "with no app found, every page shows");
        claude.Installed = codex.Installed = true;
        LookAgain(cards);
    }

    private static void LookAgain(CardSet cards)
    {
        var look = cards.CheckInstallsAsync();
        PumpUntil(() => look.IsCompleted, TimeSpan.FromSeconds(5));
        Pump(TimeSpan.FromMilliseconds(300));
    }

    /// <summary>How far the icons' middle sits from the card's, in the card's own units.</summary>
    private static double CenterOffset(MainWindow window, FrameworkElement icons)
    {
        var card = Find<Border>(window, "Card");
        return icons.TransformToAncestor(card).Transform(new Point(0, 0)).X + icons.ActualWidth / 2 - card.ActualWidth / 2;
    }

    private static void CheckDragKeepsProportions(MainWindow window, IntPtr handle, string when)
    {
        var rect = WindowRect(handle);
        var dragged = new PixelRect(rect.Left, rect.Top, rect.Left + rect.Width * 8 / 5, rect.Bottom);
        var buffer = Marshal.AllocHGlobal(Marshal.SizeOf<PixelRect>());
        try
        {
            Marshal.StructureToPtr(dragged, buffer, false);
            SendMessage(handle, WmSizing, AspectSizing.RightEdge, buffer);
            var fitted = Marshal.PtrToStructure<PixelRect>(buffer);
            var expected = (int)Math.Round(fitted.Width * (double)rect.Height / rect.Width);
            Check(Math.Abs(fitted.Height - expected) <= 2, $"with {when}, dragging the right edge grows the height too ({rect.Width}x{rect.Height} -> {fitted.Width}x{fitted.Height})");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
