using System.Text.Json;

namespace MiniInfos.Tests;

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
    public void ASplitCardTakesAnotherClickedPageInItsSecondPlace() =>
        Assert.Equal([0, 1], PageSelection.Single(0, 3).SplitWith(2).SplitWith(1).Shown);

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
