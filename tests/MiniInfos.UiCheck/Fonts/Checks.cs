namespace MiniInfos.UiCheck;

internal static partial class Program
{
    private static void CheckFonts(CardSet cards, Preferences preferences, SettingsWindow settings, string pictures)
    {
        var card = cards.Main;
        var originalTheme = preferences.Theme;
        foreach (var option in AppFonts.TextOptions.Concat(AppFonts.NumberOptions).DistinctBy(option => option.Choice))
        {
            foreach (var weight in new[] { FontWeights.Normal, FontWeights.SemiBold })
            {
                var typeface = new Typeface(option.Family, FontStyles.Normal, weight, FontStretches.Normal);
                Check(typeface.TryGetGlyphTypeface(out var glyph) && glyph.FontUri.Scheme == "pack"
                    && glyph.FamilyNames.Values.Contains(option.Name), $"{option.Name} {weight} loads from the app, without font fallback");
            }
        }

        void ChooseFont(string panel, FontChoice choice) => Find<StackPanel>(settings, panel).Children
            .OfType<RadioButton>().Single(button => (FontChoice)button.Tag == choice).IsChecked = true;

        var (_, _, _, open) = OpenPageMenu(Find<StackPanel>(card, "PageIcons"), 1);
        Choose(open);
        PumpUntil(() => cards.Extras.Count == 1 && cards.Extras[0].Opacity == 1, TimeSpan.FromSeconds(10));
        var extra = cards.Extras.Single();
        var originalWidth = card.Width;
        card.IsDetailed = true;
        var combinations = new[]
        {
            (Name: "atual", Text: FontChoice.Manrope, Number: FontChoice.Manrope),
            (Name: "monoespacada", Text: FontChoice.Manrope, Number: FontChoice.GeistMono),
            (Name: "monumental", Text: FontChoice.Cinzel, Number: FontChoice.BodoniModa),
            (Name: "digital", Text: FontChoice.ChakraPetch, Number: FontChoice.Orbitron),
        };
        foreach (var combination in combinations)
        {
            ChooseFont("TextFontChoices", combination.Text);
            ChooseFont("NumberFontChoices", combination.Number);
            Pump(TimeSpan.FromMilliseconds(500));
            var page = Columns(card)[0];
            var detail = Find<TextBlock>(page, "ShortDetail");
            Check(card.FontFamily == AppFonts.Family(combination.Text)
                && extra.FontFamily == card.FontFamily
                && Find<TextBlock>(page, "TodayTokens").FontFamily == AppFonts.Family(combination.Number)
                && Find<TextBlock>(Columns(extra)[0], "TodayTokens").FontFamily == AppFonts.Family(combination.Number),
                $"{combination.Name}: both open cards take the chosen text and number fonts immediately");
            Check(detail.Inlines.OfType<Run>().Any(run => run.Text.Any(char.IsDigit)
                    && run.FontFamily == AppFonts.Family(combination.Number))
                && detail.Inlines.OfType<Run>().Any(run => run.Text.Contains("tokens")
                    && run.FontFamily == AppFonts.Family(combination.Text)),
                "mixed captions keep letters and digits in their separate families");
            var saved = Preferences.Load();
            Check(saved.TextFont == combination.Text && saved.NumberFont == combination.Number,
                "the two font choices are saved independently");
            Check(Math.Abs(card.Width - originalWidth) < 1, "changing fonts preserves the card's scale");
            var smallCaps = combination.Text == FontChoice.Cinzel;
            Check(Find<TextBlock>(Find<CardHeader>(card, "Header"), "AppTitle").Text == (smallCaps ? "Miniinfos" : "MiniInfos")
                && Find<TextBlock>(Columns(extra)[0], "PageTitle").Text == (smallCaps ? "Codex (Gpt)" : "Codex (GPT)"),
                smallCaps ? "with Cinzel only the capitals that open a word stay (Miniinfos, Codex (Gpt))" : "other text fonts keep the names as written");
            Render(card, System.IO.Path.Combine(pictures, $"font-{combination.Name}.png"));
        }

        Render(settings, System.IO.Path.Combine(pictures, "settings-fonts.png"));
        var (_, split, _, _) = OpenPageMenu(Find<StackPanel>(card, "PageIcons"), 1);
        Choose(split);
        Check(Columns(card).All(page => Find<TextBlock>(page, "TodayTokens").FontFamily == AppFonts.Family(FontChoice.Orbitron)),
            "both split columns use the selected number font");
        Render(card, System.IO.Path.Combine(pictures, "split-digital.png"));
        ChooseTheme(settings, ThemeChoice.Light);
        Pump(TimeSpan.FromMilliseconds(300));
        Check(card.FontFamily == AppFonts.Family(FontChoice.ChakraPetch)
            && Find<TextBlock>(Columns(card)[0], "TodayTokens").FontFamily == AppFonts.Family(FontChoice.Orbitron),
            "changing the color theme leaves the font choices intact");
        preferences.Theme = originalTheme;
        var (_, _, single, _) = OpenPageMenu(Find<StackPanel>(card, "PageIcons"), 0);
        Choose(single);
        Find<Button>(Find<CardHeader>(extra, "Header"), "DismissButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        ChooseFont("TextFontChoices", FontChoice.Manrope);
        ChooseFont("NumberFontChoices", FontChoice.Manrope);
        Pump(TimeSpan.FromMilliseconds(500));
    }
}
