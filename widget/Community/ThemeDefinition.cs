namespace MiniInfos.Community;

public sealed record ThemeDefinition(
    string Base = "Simple",
    Dictionary<string, string>? Colors = null,
    string? HeaderImage = null,
    double HeaderHeight = 96,
    double HeaderTitleSize = 20)
{
    public static IReadOnlySet<string> ColorKeys { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "CardBackground", "CardBorder", "PrimaryText", "DimText", "Divider", "Track", "Hover", "Selected",
        "SelectedBorder", "IconOther", "MenuBackground", "MenuBorder", "MenuHighlight", "MenuDisabled",
        "Accent", "AccentText", "Warning", "Danger",
    };
}
