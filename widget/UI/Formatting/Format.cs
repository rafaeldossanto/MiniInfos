using System.Globalization;
using MiniInfos.Providers;

namespace MiniInfos.UI.Formatting;

public static class Format
{
    private static CultureInfo Culture => L.Culture;

    public static string Count(long value) => value switch
    {
        < 1_000 => value.ToString(Culture),
        < 1_000_000 => Scaled(value / 1_000d, "k"),
        < 1_000_000_000 => Scaled(value / 1_000_000d, "M"),
        _ => Scaled(value / 1_000_000_000d, "B"),
    };

    /// <summary>The whole number, every digit, grouped: 1.412.345.678.</summary>
    public static string Full(long value) => value.ToString("N0", Culture);

    public static string Breakdown(TokenCount count, Func<long, string> number) =>
        L.Format("entrada {0} · cache {1} · saída {2}", number(count.Input + count.CacheWrite), number(count.CacheRead), number(count.Output));

    public static string Percent(double value) => $"{Math.Round(value).ToString(Culture)}%";

    /// <summary>How a page names a plan window by its length: "Janela 5h", "Semanal".</summary>
    public static string WindowLabel(TimeSpan length) =>
        length >= TimeSpan.FromDays(7) ? L.Text("Semanal")
        : length.TotalHours >= 1 ? L.Format("Janela {0}h", length.TotalHours.ToString("0", Culture))
        : L.Format("Janela {0}min", length.TotalMinutes.ToString("0", Culture));

    /// <summary>The stretch a window with no reading counts back over: "últimas 5h", "últimos 7 dias".</summary>
    public static string WindowSpan(TimeSpan length) =>
        length.TotalDays >= 2 ? L.Format("últimos {0} dias", length.TotalDays.ToString("0", Culture))
        : length.TotalHours >= 1 ? L.Format("últimas {0}h", length.TotalHours.ToString("0", Culture))
        : L.Format("últimos {0}min", length.TotalMinutes.ToString("0", Culture));

    public static string Cost(double usd) => L.Format("US$ {0}", usd.ToString("0.00", Culture));

    public static string Reset(DateTimeOffset resetsAt, DateTimeOffset now)
    {
        var left = resetsAt - now;
        if (left <= TimeSpan.Zero) return L.Text("reiniciou");

        var minutes = (int)Math.Ceiling(left.TotalMinutes);
        var days = minutes / 1_440;
        var hours = minutes % 1_440 / 60;
        var rest = minutes % 60;

        if (days > 0) return L.Format("reinicia em {0}d {1}h", days, hours);
        if (hours > 0) return L.Format("reinicia em {0}h {1}min", hours, rest);

        return L.Format("reinicia em {0}min", rest);
    }

    public static string Ago(DateTimeOffset at, DateTimeOffset now)
    {
        var elapsed = now - at;
        if (elapsed < TimeSpan.FromMinutes(1)) return L.Text("agora");
        if (elapsed < TimeSpan.FromHours(1)) return L.Format("há {0} min", (int)elapsed.TotalMinutes);
        if (elapsed < TimeSpan.FromDays(1)) return L.Format("há {0} h", (int)elapsed.TotalHours);

        return L.Format("há {0} d", (int)elapsed.TotalDays);
    }

    private static string Scaled(double value, string unit) =>
        (value >= 100 ? value.ToString("0", Culture) : value.ToString("0.#", Culture)) + unit;
}
