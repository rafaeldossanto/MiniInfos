using System.Windows.Controls;
using System.Windows.Media;

namespace MiniInfos;

/// <summary>One provider's page, drawn from its <see cref="UsagePage"/>; it knows no provider by name.</summary>
public partial class PageView : UserControl
{
    private static readonly Brush CalmMeter = Frozen(Color.FromRgb(0x4C, 0x8D, 0xF6));
    private static readonly Brush WarningMeter = Frozen(Color.FromRgb(0xE3, 0xA3, 0x3B));
    private static readonly Brush DangerMeter = Frozen(Color.FromRgb(0xE5, 0x53, 0x4B));

    private readonly RollingCount _shortTokens;
    private readonly RollingCount _longTokens;
    private readonly RollingCount _todayTokens;
    private readonly RollingCount _totalTokens;

    public PageView()
    {
        InitializeComponent();
        _shortTokens = new RollingCount(ShortDetail);
        _longTokens = new RollingCount(LongDetail);
        _todayTokens = new RollingCount(TodayTokens);
        _totalTokens = new RollingCount(TotalTokens);
    }

    /// <summary>The page's limits in one line, as the tray icon reads them: "Janela 5h 42% · Semanal 92%".</summary>
    public string LimitsLine => $"{ShortLabel.Text} {ShortPercent.Text} · {LongLabel.Text} {LongPercent.Text}";

    /// <param name="countText">How the chosen view writes a count: 1,4B, or every digit.</param>
    /// <param name="note">A short note beside the title (how fresh the limits are), or null for none.</param>
    public void Show(UsagePage page, DateTimeOffset now, Func<long, string> countText, string? note)
    {
        PageTitle.Text = page.Title;
        Note.Text = note ?? "";
        ShowWindow(page.ShortWindow, page.TokensOrigin, now, countText, ShortLabel, ShortPercent, ShortBar, ShortDetail, _shortTokens);
        ShowWindow(page.LongWindow, page.TokensOrigin, now, countText, LongLabel, LongPercent, LongBar, LongDetail, _longTokens);

        _todayTokens.Show(page.Today.Total, countText);
        _totalTokens.Show(page.Total.Total, countText);
        TodayBlock.ToolTip = $"{Format.Breakdown(page.Today, countText)}\n{page.TokensOrigin}";
        TotalBlock.ToolTip = $"{Format.Breakdown(page.Total, countText)}\n{page.TokensOrigin}";

        ChatText.Text = page.Activity;
        ChatText.ToolTip = $"{page.Activity}\n{page.ActivityHint}";
    }

    /// <summary>Another provider moved into this column: its counts start over instead of rolling from the last one's.</summary>
    public void Restart()
    {
        foreach (var count in (RollingCount[])[_shortTokens, _longTokens, _todayTokens, _totalTokens]) count.Reset();
    }

    private static void ShowWindow(
        WindowLine line,
        string tokensOrigin,
        DateTimeOffset now,
        Func<long, string> countText,
        TextBlock label,
        TextBlock percent,
        ProgressBar bar,
        TextBlock detail,
        RollingCount used)
    {
        var length = line.Window.Length;
        label.Text = Format.WindowLabel(length);
        detail.ToolTip = $"{Format.Breakdown(line.Tokens, countText)}\n{tokensOrigin}";

        if (line.Window.Limit is not { } limit)
        {
            percent.Text = "—";
            bar.Value = 0;
            used.Show(line.Tokens.Total, value => $"{countText(value)} tokens nas {Format.WindowSpan(length)}");
            return;
        }

        var reset = limit.ResetsAtTime;
        var hasReset = reset is { } time && time <= now;
        percent.Text = hasReset ? "—" : Format.Percent(limit.PercentUsed);
        bar.Value = hasReset ? 0 : Math.Clamp(limit.PercentUsed, 0, 100);
        bar.Foreground = limit.PercentUsed switch
        {
            >= 90 => DangerMeter,
            >= 70 => WarningMeter,
            _ => CalmMeter,
        };
        var resetText = reset is { } at ? $" · {Format.Reset(at, now)}" : "";
        used.Show(line.Tokens.Total, value => $"{countText(value)} tokens{resetText}");
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
