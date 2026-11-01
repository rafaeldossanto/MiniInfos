using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MiniInfos.Providers;
using MiniInfos.Settings;
using static MiniInfos.Desktop.UI.Elements;

namespace MiniInfos.Desktop.UI;

/// <summary>The native drawing of the shared six-hour token history.</summary>
internal sealed class UsageChart : StackPanel
{
    public UsageChart(TokenHistory? history, Palette palette, Func<long, string> countText)
    {
        Height = UsageChartLayout.Height; Spacing = 0;
        var divider = Line(palette); divider.Margin = new Thickness(0, 10, 0, 5); Children.Add(divider);
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(Text(L.Text("Consumo recente"), palette, 11, true));
        var period = Text(L.Text("últimas 6h"), palette, 10, true); Grid.SetColumn(period, 1); heading.Children.Add(period); Children.Add(heading);
        var values = history?.Tokens ?? new long[TokenHistory.BucketCount];
        var peak = values.Count == 0 ? 0 : values.Max();
        var plot = new Grid { Height = 28 };
        for (var index = 0; index < values.Count; index++)
        {
            plot.ColumnDefinitions.Add(new ColumnDefinition());
            var slot = new Grid { Background = Brushes.Transparent, Margin = new Thickness(0, 0, index == values.Count - 1 ? 0 : 4, 0) };
            var bar = new Border { Height = peak > 0 ? values[index] * 26d / peak : 0, Background = palette["Accent"],
                VerticalAlignment = VerticalAlignment.Bottom, CornerRadius = new CornerRadius(2, 2, 0, 0), Opacity = .8 };
            slot.Children.Add(bar);
            if (history is not null)
            {
                var start = history.Start + TokenHistory.BucketLength * index;
                ToolTip.SetTip(slot, L.Format("{0}–{1} · {2} tokens", start.ToString("t", L.Culture), (start + TokenHistory.BucketLength).ToString("t", L.Culture), countText(values[index])));
            }
            Grid.SetColumn(slot, index); plot.Children.Add(slot);
        }
        var layers = new Grid { Margin = new Thickness(0, 4, 0, 0) }; layers.Children.Add(plot);
        var baseline = new Border { Height = 1, Background = palette["Track"], VerticalAlignment = VerticalAlignment.Bottom }; layers.Children.Add(baseline);
        if (peak == 0)
        {
            var empty = Text(L.Text("Sem consumo registrado nas últimas 6h"), palette, 10, true);
            empty.HorizontalAlignment = HorizontalAlignment.Center; empty.VerticalAlignment = VerticalAlignment.Center; layers.Children.Add(empty);
        }
        Children.Add(layers);
        var axis = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 2, 0, 0) };
        axis.Children.Add(Text(L.Text("6h atrás"), palette, 10, true)); var now = Text(L.Text("agora"), palette, 10, true);
        Grid.SetColumn(now, 1); axis.Children.Add(now); Children.Add(axis);
        Avalonia.Automation.AutomationProperties.SetName(this, L.Text("Consumo recente") + " · " + L.Text("últimas 6h"));
    }
}
