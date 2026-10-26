using System.Windows;

namespace MiniInfos;

public sealed record PetPlacement(double Left, double Top)
{
    public static double Size(PetSizeChoice choice) => choice switch
    {
        PetSizeChoice.Small => 120,
        PetSizeChoice.Large => 220,
        _ => 160,
    };

    /// <summary>A saved pet remains reachable after monitors are removed or their resolution changes.</summary>
    public PetPlacement KeepVisible(Rect screen, double size) => new(
        Clamp(Left, screen.Left, screen.Right - size),
        Clamp(Top, screen.Top, screen.Bottom - size));

    private static double Clamp(double value, double minimum, double maximum) =>
        Math.Clamp(double.IsFinite(value) ? value : minimum, minimum, Math.Max(minimum, maximum));
}
