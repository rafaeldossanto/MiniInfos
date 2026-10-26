using System.Windows;
using System.Windows.Media;
using MiniInfos.Community;
using MiniInfos.Settings;

namespace MiniInfos.UI.Themes;

public static class CommunityTheme
{
    public static ResourceDictionary Load(InstalledPackage package)
    {
        var definition = package.Manifest.Theme!;
        var resources = ThemeManager.Load(Enum.Parse<ThemeChoice>(definition.Base));
        foreach (var (key, value) in definition.Colors ?? [])
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
            brush.Freeze();
            resources[key] = brush;
        }
        if (definition.HeaderImage is { } image)
        {
            resources["HeaderArtwork"] = PackageImages.Load(PackageFiles.Resolve(package.Directory, image));
            resources["HeaderArtworkVisibility"] = Visibility.Visible;
            resources["HeaderHeight"] = definition.HeaderHeight;
            resources["HeaderTitleSize"] = definition.HeaderTitleSize;
        }
        return resources;
    }
}
