using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MiniInfos.Providers;
using MiniInfos.Settings;
using MiniInfos.UI.Formatting;

namespace MiniInfos.UI.Controls;

/// <summary>Recent local token use, normalized to the busiest twenty-minute interval.</summary>
public sealed class UsageChart : Grid
{
    private readonly TextBlock _title = Caption(11);
    private readonly TextBlock _period = Caption(10);
    private readonly TextBlock _start = Caption(10);
    private readonly TextBlock _end = Caption(10);
    private readonly TextBlock _empty = Caption(10);
    private readonly Grid _bars = new() { Height = 28, Margin = new Thickness(0, 4, 0, 0) };

    public UsageChart()
    {
        Height = UsageChartLayout.Height;
        var content = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        var divider = new Border { Height = 1, Margin = new Thickness(0, 0, 0, 5) };
        divider.SetResourceReference(Border.BackgroundProperty, "Divider"); content.Children.Add(divider);
        var heading = new Grid(); heading.Children.Add(_title);
        _period.HorizontalAlignment = HorizontalAlignment.Right; heading.Children.Add(_period); content.Children.Add(heading);
        var plot = new Grid(); plot.Children.Add(_bars);
        var baseline = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Bottom };
        baseline.SetResourceReference(Border.BackgroundProperty, "Track"); plot.Children.Add(baseline);
        _empty.HorizontalAlignment = HorizontalAlignment.Center; _empty.VerticalAlignment = VerticalAlignment.Center; plot.Children.Add(_empty);
        content.Children.Add(plot);
        var axis = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        axis.Children.Add(_start); _end.HorizontalAlignment = HorizontalAlignment.Right; axis.Children.Add(_end); content.Children.Add(axis);
        Children.Add(content);
    }

    public void Show(TokenHistory? history, Func<long, string> countText)
    {
        _title.Text = L.Text("Consumo recente"); _period.Text = L.Text("últimas 6h");
        _start.Text = L.Text("6h atrás"); _end.Text = L.Text("agora");
        _empty.Text = L.Text("Sem consumo registrado nas últimas 6h");
        var values = history?.Tokens ?? new long[TokenHistory.BucketCount];
        var peak = values.Count == 0 ? 0 : values.Max();
        _empty.Visibility = peak > 0 ? Visibility.Collapsed : Visibility.Visible;
        _bars.Children.Clear(); _bars.ColumnDefinitions.Clear();
        for (var index = 0; index < values.Count; index++)
        {
            _bars.ColumnDefinitions.Add(new ColumnDefinition());
            var slot = new Grid { Background = Brushes.Transparent, Margin = new Thickness(0, 0, index == values.Count - 1 ? 0 : 4, 0) };
            if (history is not null)
            {
                var start = history.Start + TokenHistory.BucketLength * index;
                slot.ToolTip = L.Format("{0}–{1} · {2} tokens", start.ToString("t", L.Culture), (start + TokenHistory.BucketLength).ToString("t", L.Culture), countText(values[index]));
            }
            var bar = new Border { Height = peak > 0 ? values[index] * 26d / peak : 0, VerticalAlignment = VerticalAlignment.Bottom,
                CornerRadius = new CornerRadius(2, 2, 0, 0), Opacity = .8 };
            bar.SetResourceReference(Border.BackgroundProperty, "Accent"); slot.Children.Add(bar);
            Grid.SetColumn(slot, index); _bars.Children.Add(slot);
        }
        System.Windows.Automation.AutomationProperties.SetName(this, $"{_title.Text} · {_period.Text}");
    }

    private static TextBlock Caption(double size)
    {
        var text = new TextBlock { FontSize = size, LineHeight = 15, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
        text.SetResourceReference(TextBlock.ForegroundProperty, "DimText"); return text;
    }
}
