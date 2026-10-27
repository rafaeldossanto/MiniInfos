namespace MiniInfos.Settings;

/// <summary>
/// The themes in the order they were last chosen, built-in and community alike: the theme row shows the latest first.
/// A built-in is kept as "builtin:Classic"; a community theme by its package id, which can never hold a colon.
/// </summary>
public static class ThemeHistory
{
    /// <summary>Enough for the row and the library's order; older choices fall off.</summary>
    public const int Kept = 12;

    private const string BuiltInPrefix = "builtin:";

    public static string Key(ThemeChoice theme) => BuiltInPrefix + theme;

    /// <summary>The built-in a key names, or null for a community theme's id.</summary>
    public static ThemeChoice? BuiltIn(string key) =>
        key.StartsWith(BuiltInPrefix, StringComparison.Ordinal) && Enum.TryParse<ThemeChoice>(key[BuiltInPrefix.Length..], out var theme)
            && Enum.IsDefined(theme) ? theme : null;

    /// <summary>
    /// The key moved to the front, once, the list cut to the last <see cref="Kept"/>. When it already leads, the same
    /// list comes back, so choosing the theme in use leaves the settings equal and saves nothing.
    /// </summary>
    public static IReadOnlyList<string> Promote(IReadOnlyList<string>? recent, string key) =>
        recent is [var first, ..] && first == key ? recent : [key, .. (recent ?? []).Where(other => other != key).Take(Kept - 1)];

    /// <summary>
    /// Every available theme, the last chosen first; those never chosen follow in the order given. A remembered
    /// theme that is no longer installed is skipped.
    /// </summary>
    public static IReadOnlyList<string> Order(IReadOnlyList<string>? recent, IReadOnlyList<string> available) =>
        [.. (recent ?? []).Where(available.Contains).Concat(available).Distinct()];
}
