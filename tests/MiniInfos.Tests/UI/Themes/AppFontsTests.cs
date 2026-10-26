using MiniInfos.Settings;
using MiniInfos.Tests.Support;
using MiniInfos.UI.Themes;

namespace MiniInfos.Tests.UI.Themes;

public sealed class AppFontsTests
{
    [Theory]
    [InlineData(FontChoice.Manrope, FontChoice.Manrope)]
    [InlineData(FontChoice.Manrope, FontChoice.GeistMono)]
    [InlineData(FontChoice.Cinzel, FontChoice.BodoniModa)]
    [InlineData(FontChoice.ChakraPetch, FontChoice.Orbitron)]
    public void ChosenFontsSurviveAThemeChangeAndRestart(FontChoice text, FontChoice numbers)
    {
        using var folder = new TempFolder();
        var path = folder.File("settings.json");
        var preferences = new Preferences(path) { TextFont = text, NumberFont = numbers };
        preferences.Theme = ThemeChoice.Classic;

        var reloaded = new Preferences(path);

        Assert.Equal((text, numbers, ThemeChoice.Classic), (reloaded.TextFont, reloaded.NumberFont, reloaded.Theme));
    }

    [Theory]
    [InlineData("MiniInfos", "Miniinfos")]
    [InlineData("Codex (GPT)", "Codex (Gpt)")]
    [InlineData("MiniInfos · contexto 38% · US$ 87,93", "Miniinfos · contexto 38% · Us$ 87,93")]
    [InlineData("Janela 5h", "Janela 5h")]
    [InlineData("atualizado há 6 min", "atualizado há 6 min")]
    public void ASmallCapsFontKeepsOnlyTheCapitalsThatOpenAWord(string text, string shown) =>
        Assert.Equal(shown, AppFonts.InitialCapitalsOnly(text));
}
