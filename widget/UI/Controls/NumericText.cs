using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Documents;
using MiniInfos.UI.Formatting;
using MiniInfos.UI.Themes;

namespace MiniInfos.UI.Controls;

/// <summary>
/// Mixed labels keep their text family while amounts, percentages and times use the number family; the
/// letters take the capitals the text font wants (<see cref="AppFonts.Cased"/>).
/// </summary>
public static partial class NumericText
{
    public static void Show(TextBlock target, string text)
    {
        var value = AppFonts.Cased(text);
        if (target.Text == value) return;

        target.Inlines.Clear();
        var position = 0;
        foreach (Match match in NumberPattern().Matches(value))
        {
            if (match.Index > position) target.Inlines.Add(new Run(value[position..match.Index]));
            var number = new Run(match.Value);
            number.SetResourceReference(TextElement.FontFamilyProperty, "NumberFont");
            target.Inlines.Add(number);
            position = match.Index + match.Length;
        }

        if (position < value.Length) target.Inlines.Add(new Run(value[position..]));
    }

    /// <summary>A number with its grouping and its unit, as Format writes them: 12.345, 1,4B, 51,2k, 42%.</summary>
    [GeneratedRegex(@"\d+(?:[.,]\d+)*(?:[%kMB])?")]
    private static partial Regex NumberPattern();
}
