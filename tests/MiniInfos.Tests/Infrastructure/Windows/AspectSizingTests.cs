using MiniInfos.Infrastructure.Windows;

namespace MiniInfos.Tests.Infrastructure.Windows;

public sealed class AspectSizingTests
{
    [Theory]
    [InlineData(AspectSizing.TopLeftCorner)]
    [InlineData(AspectSizing.TopRightCorner)]
    [InlineData(AspectSizing.BottomLeftCorner)]
    [InlineData(AspectSizing.BottomRightCorner)]
    public void CornersFollowVerticalOrHorizontalMotionAndKeepTheOppositeCornerFixed(int edge)
    {
        var initial = new PixelRect(100, 100, 500, 300);
        var pullsLeft = edge is AspectSizing.TopLeftCorner or AspectSizing.BottomLeftCorner;
        var pullsTop = edge is AspectSizing.TopLeftCorner or AspectSizing.TopRightCorner;
        foreach (var (dx, dy) in new[] { (0, 50), (100, 0), (30, 50), (0, -40), (-80, 0) })
        {
            var proposed = new PixelRect(initial.Left - (pullsLeft ? dx : 0), initial.Top - (pullsTop ? dy : 0),
                initial.Right + (pullsLeft ? 0 : dx), initial.Bottom + (pullsTop ? 0 : dy));
            var fitted = AspectSizing.Fit(proposed, edge, 2, 100, 1000, initial);
            var expectedWidth = Math.Abs(dy) * 2 > Math.Abs(dx) ? 400 + dy * 2 : 400 + dx;
            Assert.Equal((expectedWidth, expectedWidth / 2), (fitted.Width, fitted.Height));
            Assert.Equal(pullsLeft ? initial.Right : initial.Left, pullsLeft ? fitted.Right : fitted.Left);
            Assert.Equal(pullsTop ? initial.Bottom : initial.Top, pullsTop ? fitted.Bottom : fitted.Top);
        }
    }

    [Theory]
    [InlineData(AspectSizing.TopEdge)]
    [InlineData(AspectSizing.BottomEdge)]
    public void VerticalEdgesScaleBothDimensionsAndRespectLimits(int edge)
    {
        var origin = new PixelRect(100, 100, 500, 300);
        var pullsTop = edge == AspectSizing.TopEdge;
        foreach (var height in new[] { 50, 250, 700 })
        {
            var proposed = new PixelRect(100, pullsTop ? 300 - height : 100, 500, pullsTop ? 300 : 100 + height);
            var fitted = AspectSizing.Fit(proposed, edge, 2, 200, 1000, origin);
            Assert.Equal(Math.Clamp(height * 2, 200, 1000), fitted.Width);
            Assert.Equal(fitted.Width / 2, fitted.Height);
            Assert.Equal(pullsTop ? origin.Bottom : origin.Top, pullsTop ? fitted.Bottom : fitted.Top);
        }
    }

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
