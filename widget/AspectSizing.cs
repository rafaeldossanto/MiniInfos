using System.Runtime.InteropServices;

namespace MiniInfos;

/// <summary>A window rectangle in screen pixels, laid out as Win32's RECT.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct PixelRect(int left, int top, int right, int bottom)
{
    public int Left = left;
    public int Top = top;
    public int Right = right;
    public int Bottom = bottom;

    public readonly int Width => Right - Left;

    public readonly int Height => Bottom - Top;
}

/// <summary>
/// Keeps the window's proportions while the person drags an edge or a corner (WM_SIZING),
/// so the card grows and shrinks as a whole instead of leaving empty space beside it.
/// </summary>
public static class AspectSizing
{
    public const int LeftEdge = 1;
    public const int RightEdge = 2;
    public const int TopEdge = 3;
    public const int TopLeftCorner = 4;
    public const int TopRightCorner = 5;
    public const int BottomEdge = 6;
    public const int BottomLeftCorner = 7;
    public const int BottomRightCorner = 8;

    /// <param name="aspect">Width over height.</param>
    public static PixelRect Fit(PixelRect rect, int edge, double aspect, int minWidth, int maxWidth)
    {
        var leadsByHeight = edge is TopEdge or BottomEdge;
        var width = leadsByHeight ? (int)Math.Round(rect.Height * aspect) : rect.Width;
        width = Math.Clamp(width, minWidth, maxWidth);
        var height = (int)Math.Round(width / aspect);

        var left = edge is LeftEdge or TopLeftCorner or BottomLeftCorner ? rect.Right - width : rect.Left;
        var top = edge is TopEdge or TopLeftCorner or TopRightCorner ? rect.Bottom - height : rect.Top;

        return new PixelRect(left, top, left + width, top + height);
    }
}
