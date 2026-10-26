using MiniInfos.Providers;

namespace MiniInfos.Settings;

/// <summary>Where a card was left, how big, and what it shows.</summary>
/// <param name="Scale">The card's size over its design size.</param>
/// <param name="IsDetailed">Whole numbers rolling up, instead of the short 1,4B form.</param>
/// <param name="Page">The id of the provider whose page was on screen (<see cref="IUsageProvider.Id"/>).</param>
/// <param name="BesidePage">In "Visualização dividida", the id of the page shown beside it.</param>
public sealed record CardSettings(
    double Left,
    double Top,
    double Scale = 1,
    bool IsDetailed = false,
    string? Page = null,
    string? BesidePage = null);
