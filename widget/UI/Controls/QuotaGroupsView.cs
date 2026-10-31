using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MiniInfos.Providers;
using MiniInfos.UI.Formatting;

namespace MiniInfos.UI.Controls;

/// <summary>Independent model groups with compact window meters; all colors and fonts follow the card theme.</summary>
public sealed class QuotaGroupsView : StackPanel
{
    private sealed record MeterParts(TextBlock Label, TextBlock Percent, ProgressBar Bar, TextBlock? Detail);
    private readonly List<(UsageQuotaGroup Group, IReadOnlyList<MeterParts> Meters)> _parts = [];
    private string? _shape;
    public string LimitsLine { get; private set; } = "";

    /// <returns>Whether the card needs measuring again because its content changed shape.</returns>
    public bool Show(IReadOnlyList<UsageQuotaGroup> groups, DateTimeOffset now)
    {
        var shape = string.Join("\n", groups.Select(group => group.Title + "|" + group.Models + "|" + string.Join(',', group.Windows.Select(window => window.Length.Ticks))));
        var changed = shape != _shape;
        if (changed) Build(groups);
        _shape = shape;
        var summary = new List<string>();
        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            var labels = new List<string>();
            for (var meter = 0; meter < group.Windows.Count; meter++)
            {
                var window = group.Windows[meter];
                var parts = _parts[index].Meters[meter];
                var label = window.Length == TimeSpan.FromDays(1) ? L.Text("Diário") : Format.WindowLabel(window.Length);
                NumericText.Show(parts.Label, label);
                var limit = window.Limit;
                var active = limit is not null && (limit.ResetsAtTime is not { } reset || reset > now);
                var used = active ? Math.Clamp(limit!.PercentUsed, 0, 100) : 0;
                var percent = active ? Format.Percent(used) : "—";
                NumericText.Show(parts.Percent, percent);
                parts.Bar.Value = used;
                parts.Bar.SetResourceReference(Control.ForegroundProperty, used >= 90 ? "Danger" : used >= 70 ? "Warning" : "Accent");
                var detail = active ? L.Format("{0} restante", Format.Percent(100 - used)) : L.Text("Limite indisponível");
                if (limit?.ResetsAtTime is { } at) detail += " · " + Format.Reset(at, now);
                parts.Bar.ToolTip = parts.Label.ToolTip = parts.Percent.ToolTip = detail;
                if (parts.Detail is { } caption) NumericText.Show(caption, detail);
                labels.Add(label + " " + percent);
            }
            summary.Add(group.Title + ": " + string.Join(" · ", labels));
        }
        LimitsLine = string.Join("\n", summary);
        return changed;
    }

    private void Build(IReadOnlyList<UsageQuotaGroup> groups)
    {
        Children.Clear(); _parts.Clear();
        foreach (var group in groups)
        {
            var divider = new Border { Height = 1, Margin = new Thickness(0, Children.Count == 0 ? 0 : 8, 0, 6) };
            divider.SetResourceReference(Border.BackgroundProperty, "Divider"); Children.Add(divider);
            var title = new TextBlock { FontWeight = FontWeights.SemiBold, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = group.Title };
            NumericText.Show(title, group.Title); Children.Add(title);
            if (!string.IsNullOrEmpty(group.Models))
            {
                var models = Caption(); models.Margin = new Thickness(0, 1, 0, 0);
                NumericText.Show(models, group.Models); models.ToolTip = group.Models; Children.Add(models);
            }
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 0) };
            var meters = new List<MeterParts>();
            foreach (var window in group.Windows)
            {
                var index = meters.Count;
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                var stack = new StackPanel { Margin = new Thickness(index == 0 ? 0 : 6, 0, index < group.Windows.Count - 1 ? 6 : 0, 0) };
                var heading = new Grid();
                heading.ColumnDefinitions.Add(new ColumnDefinition()); heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var label = new TextBlock { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
                var percent = new TextBlock { FontSize = 12, Margin = new Thickness(3, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
                percent.SetResourceReference(TextBlock.FontFamilyProperty, "NumberFont");
                Grid.SetColumn(percent, 1); heading.Children.Add(label); heading.Children.Add(percent); stack.Children.Add(heading);
                var bar = new ProgressBar { Style = (Style)FindResource("Meter"), Height = 3, Margin = new Thickness(0, 3, 0, 0) };
                stack.Children.Add(bar);
                TextBlock? detail = null;
                if (group.Windows.Count == 1)
                {
                    detail = Caption(); detail.Margin = new Thickness(0, 4, 0, 0); stack.Children.Add(detail);
                }
                Grid.SetColumn(stack, index); grid.Children.Add(stack); meters.Add(new MeterParts(label, percent, bar, detail));
            }
            Children.Add(grid); _parts.Add((group, meters));
        }
    }

    private TextBlock Caption()
    {
        var text = new TextBlock { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
        text.SetResourceReference(TextBlock.ForegroundProperty, "DimText"); return text;
    }
}
