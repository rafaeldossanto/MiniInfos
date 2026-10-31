using MiniInfos.Providers;
using System.Text.Json.Serialization;

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
    string? BesidePage = null,
    IReadOnlyList<string>? Pages = null)
{
    // Keep Page/BesidePage readable by older versions; newer versions restore the complete ordered selection.
    [JsonIgnore]
    public IReadOnlyList<string> SelectedPages => (Pages is { Count: > 0 } ? Pages : [Page ?? "", BesidePage ?? ""])
        .Where(page => !string.IsNullOrWhiteSpace(page)).Distinct(StringComparer.Ordinal).ToArray();
}
