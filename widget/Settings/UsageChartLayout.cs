namespace MiniInfos.Settings;

/// <summary>Charts fill spare space in their own grid row, or expand every page when requested.</summary>
public static class UsageChartLayout
{
    public const double Height = 80;

    public static bool[] Fit(IReadOnlyList<double> pageHeights, int columns, bool always)
    {
        var shown = new bool[pageHeights.Count];
        for (var row = 0; row < pageHeights.Count; row += columns)
        {
            var end = Math.Min(row + columns, pageHeights.Count);
            var tallest = 0d;
            for (var page = row; page < end; page++) tallest = Math.Max(tallest, pageHeights[page]);
            for (var page = row; page < end; page++) shown[page] = always || tallest - pageHeights[page] >= Height;
        }
        return shown;
    }
}
