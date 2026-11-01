using MiniInfos.Settings;

namespace MiniInfos.Tests.UI.Windows;

public sealed class UsageChartLayoutTests
{
    [Fact]
    public void AutomaticChartsOnlyFillEnoughSpaceInTheirOwnRow()
    {
        Assert.Equal([true, false, false, false, false], UsageChartLayout.Fit([250, 350, 250, 250, 250], 2, false));
        Assert.Equal([false, false], UsageChartLayout.Fit([271, 350], 2, false));
        Assert.Equal([false], UsageChartLayout.Fit([250], 1, false));
    }

    [Fact]
    public void AlwaysShowsTheChartEvenOnSinglePagesAndTheTallestPage()
    {
        Assert.All(UsageChartLayout.Fit([250, 350, 260], 2, true), Assert.True);
        Assert.Equal([true], UsageChartLayout.Fit([350], 1, true));
    }
}
