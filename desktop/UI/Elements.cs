using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MiniInfos.Desktop.UI;

internal static class Elements
{
    public static TextBlock Text(string value, Palette palette, double size = 12, bool dim = false, bool number = false) => new()
    {
        Text = value, FontSize = size, Foreground = palette[dim ? "DimText" : "PrimaryText"],
        FontFamily = number ? palette.NumberFont : palette.TextFont, TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center,
    };
    public static Button Button(string value, Action action, string? hint = null)
    {
        var button = new Button { Content = value, Padding = new Thickness(8, 3), MinHeight = 26 };
        button.Click += (_, _) => action();
        if (hint is not null) ToolTip.SetTip(button, hint);
        return button;
    }
    public static Border Line(Palette palette) => new() { Height = 1, Background = palette["Divider"], Margin = new Thickness(0, 9) };
    public static StackPanel Stack(params Control[] children)
    {
        var panel = new StackPanel { Spacing = 6 };
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }
    public static StackPanel Row(params Control[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }
    public static MenuItem Menu(string title, Action action, bool? selected = null)
    {
        var item = new MenuItem { Header = L.Text(title) };
        if (selected is { } check) { item.ToggleType = MenuItemToggleType.CheckBox; item.IsChecked = check; }
        item.Click += (_, _) => action();
        return item;
    }
}
