using System.Text.Json;
using MiniInfos.Settings;
using MiniInfos.UI.Windows;

namespace MiniInfos.Tests.UI.Windows;

public sealed class PageSelectionTests
{
    [Fact]
    public void ASinglePageTurnsAroundBothEnds()
    {
        var selection = PageSelection.Single(0, 2);

        Assert.Equal([1], selection.Turn(1).Shown);
        Assert.Equal([1], selection.Turn(-1).Shown);
        Assert.True(selection.CanTurn);
    }

    [Fact]
    public void SplittingPutsTheClickedPageBesideTheOneOnScreen()
    {
        var split = PageSelection.Single(0, 3).SplitWith(2);

        Assert.True(split.IsSplit);
        Assert.Equal([0, 2], split.Shown);
        Assert.True(split.Shows(0) && split.Shows(2) && !split.Shows(1));
    }

    [Fact]
    public void SplittingFromThePageOnScreenBringsTheNextOneBeside() =>
        Assert.Equal([1, 0], PageSelection.Single(1, 2).SplitWith(1).Shown);

    [Fact]
    public void AddingAnotherPageKeepsEveryPageAlreadySelected() =>
        Assert.Equal([0, 2, 1], PageSelection.Single(0, 3).SplitWith(2).SplitWith(1).Shown);

    [Fact]
    public void RepeatedAddsDoNotDuplicatePagesAndRemovingDoesNotCloseOtherPages()
    {
        var selected = PageSelection.Single(0, 5).SplitWith(2).SplitWith(4).SplitWith(1).SplitWith(3);
        Assert.Equal([0, 2, 4, 1, 3], selected.Shown);
        Assert.False(selected.CanTurn);
        Assert.True(selected.SplitWith(2).SameAs(selected));
        Assert.Equal([0, 4, 1, 3], selected.Remove(2).Shown);
        Assert.Equal([2, 4, 1, 3], selected.Remove(0).Shown);
        Assert.Equal([0], PageSelection.Single(0, 5).Remove(0).Shown);
    }

    [Fact]
    public void RestoringAGridFiltersRemovedAndRepeatedProvidersWithoutChangingOrder() =>
        Assert.Equal([3, 0, 2], PageSelection.Restore([3, -1, 0, 3, 8, 2], 4).Shown);

    [Fact]
    public void GridSettingsRoundtripAndOlderSplitSettingsStillRestore()
    {
        var grid = new CardSettings(10, 20, 1.2, true, "codex", "claude", ["codex", "claude", "gemini", "antigravity"]);
        var restored = JsonSerializer.Deserialize<CardSettings>(JsonSerializer.Serialize(grid))!;
        Assert.Equal(grid.SelectedPages, restored.SelectedPages);
        Assert.Equal("codex", restored.Page);
        Assert.Equal("claude", restored.BesidePage);
        var legacy = JsonSerializer.Deserialize<CardSettings>("""{"Left":0,"Top":0,"Page":"codex","BesidePage":"claude"}""")!;
        Assert.Equal(["codex", "claude"], legacy.SelectedPages);
    }

    [Theory]
    [InlineData(1, 1200, 1, 1)]
    [InlineData(2, 1200, 2, 1)]
    [InlineData(4, 900, 2, 2)]
    [InlineData(4, 2400, 2, 2)]
    [InlineData(5, 900, 3, 2)]
    [InlineData(12, 600, 2, 6)]
    [InlineData(3, 200, 1, 3)]
    public void CardsWrapIntoBalancedRowsInsteadOfLimitingTheSelection(int pages, double available, int columns, int rows)
    {
        var layout = CardGridLayout.Fit(pages, available, 296, 287);
        Assert.Equal(columns, layout.Columns);
        Assert.Equal(rows, layout.Rows);
    }

    [Fact]
    public void WithOneProviderThereIsNothingToSplitWith() =>
        Assert.False(PageSelection.Single(0, 1).SplitWith(0).IsSplit);

    [Fact]
    public void ASplitCardTurnsItsLastPageToOneNotOnScreen()
    {
        var split = PageSelection.Single(0, 3).SplitWith(1);

        Assert.Equal([0, 2], split.Turn(1).Shown);
        Assert.Equal([0, 2], split.Turn(-1).Shown);
    }

    [Fact]
    public void WithEveryPageOnScreenTurningStaysPut()
    {
        var split = PageSelection.Single(0, 2).SplitWith(1);

        Assert.False(split.CanTurn);
        Assert.True(split.Turn(1).SameAs(split));
    }

    [Fact]
    public void ARestoredSelectionKeepsOnlyPagesThatStillExist()
    {
        Assert.Equal([1, 0], PageSelection.Restore(1, 0, 2).Shown);
        Assert.Equal([0], PageSelection.Restore(-1, -1, 2).Shown);
        Assert.Equal([0], PageSelection.Restore(0, 5, 2).Shown);
    }

    [Fact]
    public void SettingsSavedBeforeSplitViewStillRead()
    {
        // window.json of 1.0.0 and 1.1.0: no page beside, the one page by id or none at all.
        var settings = JsonSerializer.Deserialize<CardSettings>("""{"Left":10,"Top":20,"Scale":1.2,"IsDetailed":true,"Page":"codex"}""")!;

        Assert.Equal("codex", settings.Page);
        Assert.Null(settings.BesidePage);
        Assert.Equal(1.2, settings.Scale);
    }
}
