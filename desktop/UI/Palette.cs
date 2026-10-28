using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using MiniInfos.Community;
using MiniInfos.Settings;
using System.Reflection;
using System.Xml.Linq;

namespace MiniInfos.Desktop.UI;

/// <summary>Reads only MiniInfos' trusted palette XML. Community packages supply validated data, never XAML.</summary>
internal sealed class Palette
{
    private readonly Dictionary<string, IBrush> _brushes = [];
    public IBrush this[string key] => _brushes[key];
    public Bitmap? Artwork { get; private set; }
    public double HeaderHeight { get; private set; } = 32;
    public double TitleSize { get; private set; } = 13;
    public bool Inset { get; private set; }
    public bool Light { get; private set; }
    public FontFamily TextFont { get; }
    public FontFamily NumberFont { get; }

    public Palette(Preferences preferences)
    {
        var choice = preferences.Theme;
        if (choice == ThemeChoice.System) choice = Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Light ? ThemeChoice.Light : ThemeChoice.Simple;
        Light = choice is ThemeChoice.Light or ThemeChoice.ClassicLight;
        Read("Simple");
        if (Light) Read("Light");
        if (choice is ThemeChoice.Classic or ThemeChoice.ClassicDark) Read("Classic");
        if (choice is ThemeChoice.ClassicDark or ThemeChoice.ClassicLight) Read(choice.ToString());
        var custom = PackageLibrary.Shared.Find(PackageKind.Theme, preferences.CommunityThemeId);
        if (custom?.Manifest.Theme is { } definition)
        {
            Read("Simple"); Read(definition.Base);
            Light = definition.Base == "Light";
            foreach (var (key, color) in definition.Colors ?? []) _brushes[key] = Brush.Parse(color);
            if (definition.HeaderImage is { } image) Artwork = PackageImages.Load(PackageFiles.Resolve(custom.Directory, image));
            HeaderHeight = definition.HeaderHeight; TitleSize = definition.HeaderTitleSize; Inset = false;
        }
        TextFont = Family(preferences.TextFont); NumberFont = Family(preferences.NumberFont);
    }

    public static FontFamily Family(FontChoice choice) => new("avares://MiniInfos.Desktop/Assets/Fonts#" + (choice switch
    {
        FontChoice.GeistMono => "Geist Mono", FontChoice.BodoniModa => "Bodoni Moda", FontChoice.ChakraPetch => "Chakra Petch", _ => choice.ToString(),
    }));

    private void Read(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Themes." + name + ".xaml")!;
        var document = XDocument.Load(stream);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        foreach (var element in document.Root!.Elements())
        {
            var key = (string?)element.Attribute(x + "Key");
            if (key is null) continue;
            if (element.Name.LocalName == "SolidColorBrush") _brushes[key] = Brush.Parse((string)element.Attribute("Color")!);
            if (key == "HeaderHeight") HeaderHeight = double.Parse(element.Value, System.Globalization.CultureInfo.InvariantCulture);
            if (key == "HeaderTitleSize") TitleSize = double.Parse(element.Value, System.Globalization.CultureInfo.InvariantCulture);
            if (key == "HeaderArtworkInset") Inset = bool.Parse(element.Value);
            if (key == "HeaderArtwork")
            {
                var source = (string)element.Attribute("UriSource")!;
                using var image = AssetLoader.Open(new Uri("avares://MiniInfos.Desktop/Assets/" + source.Split('/').Last()));
                Artwork = new Bitmap(image);
            }
        }
    }
}
