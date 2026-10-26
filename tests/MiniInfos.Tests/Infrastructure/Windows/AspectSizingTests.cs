using MiniInfos.Infrastructure.Windows;

namespace MiniInfos.Tests.Infrastructure.Windows;

public sealed class AspectSizingTests
{
    [Fact]
    public void DraggingAnEdgeKeepsTheCardsProportions()
    {
        const double aspect = 264.0 / 228.0;

        var right = AspectSizing.Fit(new PixelRect(100, 100, 500, 200), AspectSizing.RightEdge, aspect, 198, 792);
        Assert.Equal((400, (int)Math.Round(400 / aspect)), (right.Width, right.Height));

        var corner = AspectSizing.Fit(new PixelRect(50, 50, 364, 328), AspectSizing.TopLeftCorner, aspect, 198, 792);
        Assert.Equal((364, 328), (corner.Right, corner.Bottom));

        Assert.Equal(792, AspectSizing.Fit(new PixelRect(0, 0, 2_000, 10), AspectSizing.RightEdge, aspect, 198, 792).Width);
        Assert.Equal(198, AspectSizing.Fit(new PixelRect(300, 0, 364, 10), AspectSizing.LeftEdge, aspect, 198, 792).Width);
    }
}
