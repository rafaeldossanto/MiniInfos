using MiniInfos.Settings;

namespace MiniInfos.Tests.Settings;

/// <summary>The order of the Tema row and of the library: the last chosen first.</summary>
public sealed class ThemeHistoryTests
{
    private static readonly string Simple = ThemeHistory.Key(ThemeChoice.Simple);
    private static readonly string Classic = ThemeHistory.Key(ThemeChoice.Classic);
    private static readonly string Light = ThemeHistory.Key(ThemeChoice.Light);

    [Fact]
    public void AChosenThemeMovesToTheFrontOnce() =>
        Assert.Equal([Light, Simple, Classic], ThemeHistory.Promote([Simple, Light, Classic], Light));

    [Fact]
    public void TheThemeThatAlreadyLeadsKeepsTheSameList()
    {
        IReadOnlyList<string> recent = [Light, Simple];

        Assert.Same(recent, ThemeHistory.Promote(recent, Light));
    }

    [Fact]
    public void OnlyTheLastChoicesAreKept()
    {
        IReadOnlyList<string> recent = [.. Enumerable.Range(0, ThemeHistory.Kept).Select(index => $"community.theme-{index}")];

        var promoted = ThemeHistory.Promote(recent, Classic);

        Assert.Equal((ThemeHistory.Kept, Classic), (promoted.Count, promoted[0]));
        Assert.DoesNotContain(recent[^1], promoted);
    }

    [Fact]
    public void TheOrderPutsTheChosenFirstSkipsWhatIsGoneAndKeepsTheRest() =>
        Assert.Equal([Light, "community.ocean", Simple, Classic],
            ThemeHistory.Order(["community.removed", Light, "community.ocean"], [Simple, Classic, Light, "community.ocean"]));

    [Theory]
    [InlineData("builtin:Classic", ThemeChoice.Classic)]
    [InlineData("builtin:System", ThemeChoice.System)]
    public void ABuiltInKeyNamesItsTheme(string key, ThemeChoice theme) => Assert.Equal(theme, ThemeHistory.BuiltIn(key));

    [Theory]
    [InlineData("community.ocean")]
    [InlineData("builtin:Neon")]
    [InlineData("builtin:7")]
    public void AnyOtherKeyIsNoBuiltIn(string key) => Assert.Null(ThemeHistory.BuiltIn(key));
}
