using System.Globalization;

namespace MiniInfos;

public static class Format
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Count(long value) => value switch
    {
        < 1_000 => value.ToString(PtBr),
        < 1_000_000 => Scaled(value / 1_000d, "k"),
        < 1_000_000_000 => Scaled(value / 1_000_000d, "M"),
        _ => Scaled(value / 1_000_000_000d, "B"),
    };

    /// <summary>The whole number, every digit, grouped: 1.412.345.678.</summary>
    public static string Full(long value) => value.ToString("N0", PtBr);

    public static string Breakdown(TokenCount count, Func<long, string> number) =>
        $"entrada {number(count.Input + count.CacheWrite)} · cache {number(count.CacheRead)} · saída {number(count.Output)}";

    public static string Percent(double value) => $"{Math.Round(value).ToString(PtBr)}%";

    public static string Cost(double usd) => $"US$ {usd.ToString("0.00", PtBr)}";

    public static string Reset(DateTimeOffset resetsAt, DateTimeOffset now)
    {
        var left = resetsAt - now;
        if (left <= TimeSpan.Zero) return "reiniciou";

        var minutes = (int)Math.Ceiling(left.TotalMinutes);
        var days = minutes / 1_440;
        var hours = minutes % 1_440 / 60;
        var rest = minutes % 60;

        if (days > 0) return $"reinicia em {days}d {hours}h";
        if (hours > 0) return $"reinicia em {hours}h {rest}min";

        return $"reinicia em {rest}min";
    }

    public static string Ago(DateTimeOffset at, DateTimeOffset now)
    {
        var elapsed = now - at;
        if (elapsed < TimeSpan.FromMinutes(1)) return "agora";
        if (elapsed < TimeSpan.FromHours(1)) return $"há {(int)elapsed.TotalMinutes} min";
        if (elapsed < TimeSpan.FromDays(1)) return $"há {(int)elapsed.TotalHours} h";

        return $"há {(int)elapsed.TotalDays} d";
    }

    private static string Scaled(double value, string unit) =>
        (value >= 100 ? value.ToString("0", PtBr) : value.ToString("0.#", PtBr)) + unit;
}
