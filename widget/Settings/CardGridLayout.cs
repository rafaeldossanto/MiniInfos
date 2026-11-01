namespace MiniInfos.Settings;

/// <summary>Balanced rows within the available width. Shared by the Windows and native renderers.</summary>
public sealed record CardGridLayout(int Columns, int Rows)
{
    public static CardGridLayout Fit(int pages, double availableWidth, double singleWidth, double columnStep)
    {
        if (pages < 1) throw new ArgumentOutOfRangeException(nameof(pages));
        var capacity = Math.Max(1, (int)Math.Floor((Math.Max(singleWidth, availableWidth) - singleWidth) / columnStep) + 1);
        capacity = Math.Min(capacity, (int)Math.Ceiling(Math.Sqrt(pages)));
        var rows = (int)Math.Ceiling((double)pages / capacity);
        return new((int)Math.Ceiling((double)pages / rows), rows);
    }
}
