using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MiniInfos.Community;

namespace MiniInfos.UI.Controls;

/// <summary>
/// A package as the Comunidade page shows one, and the theme library too: the preview, the name, a line per caption
/// and one button. The styles come from the window that holds it, which merges the settings' styles.
/// </summary>
public static class PackageCard
{
    /// <summary>Where the preview goes, empty until it is drawn or downloaded.</summary>
    public static Image Preview()
    {
        var image = new Image { Width = PackagePreviews.Size, Height = PackagePreviews.Size, Margin = new Thickness(0, 0, 0, 8) };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        return image;
    }

    /// <param name="status">A line under the button for what it did, when the card has one.</param>
    public static Border Create(FrameworkElement host, Image preview, string name, IEnumerable<string> captions, Button action, TextBlock? status = null)
    {
        var body = new StackPanel();
        body.Children.Add(preview);
        body.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        foreach (var caption in captions) body.Children.Add(Caption(host, caption));
        action.Style = (Style)host.FindResource("SettingsButton");
        action.Margin = new Thickness(0, 12, 0, 0);
        body.Children.Add(action);
        if (status is not null) body.Children.Add(status);
        return new Border { Style = (Style)host.FindResource("CatalogCard"), Child = body };
    }

    public static TextBlock Caption(FrameworkElement host, string text) => new()
    {
        Text = text, Style = (Style)host.FindResource("Caption"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0),
    };
}
