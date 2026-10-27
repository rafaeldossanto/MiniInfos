using System.Windows;
using MiniInfos.Community;
using MiniInfos.Settings;

namespace MiniInfos.UI.Themes;

/// <summary>
/// A theme to choose in the Tema row and in the library: one of the built-ins, or an installed community package.
/// Its key is the one <see cref="ThemeHistory"/> remembers; <see cref="Resources"/> loads the brushes it dresses the cards in.
/// </summary>
public sealed record ThemeOption(string Key, string Name, string Origin, Func<ResourceDictionary> Resources)
{
    private static string Included => L.Text("Incluído no MiniInfos");

    /// <summary>The built-ins in the order the row first shows them, before anything is chosen.</summary>
    private static IReadOnlyList<ThemeOption> BuiltIns =>
    [
        BuiltIn(ThemeChoice.Simple, L.Text("Simples"), Included),
        BuiltIn(ThemeChoice.Classic, L.Text("Clássico"), Included),
        BuiltIn(ThemeChoice.Light, L.Text("Claro"), Included),
        BuiltIn(ThemeChoice.System, "Windows", L.Text("Segue o modo claro ou escuro do Windows")),
        BuiltIn(ThemeChoice.ClassicLight, L.Text("Clássico claro"), Included),
        BuiltIn(ThemeChoice.ClassicDark, L.Text("Clássico escuro"), Included),
    ];

    /// <summary>Every theme there is to choose, the one in use first and then the last chosen.</summary>
    public static IReadOnlyList<ThemeOption> Available(Preferences preferences, PackageLibrary library)
    {
        var options = BuiltIns.Concat(library.List(PackageKind.Theme).Select(Community)).ToDictionary(option => option.Key);
        return [.. ThemeHistory.Order(preferences.RecentThemes, [.. options.Keys]).Select(key => options[key])];
    }

    private static ThemeOption BuiltIn(ThemeChoice theme, string name, string origin) =>
        new(ThemeHistory.Key(theme), name, origin, () => ThemeManager.Load(theme));

    private static ThemeOption Community(InstalledPackage package) =>
        new(package.Id, package.Name, package.Attribution, () => CommunityTheme.Load(package));
}
