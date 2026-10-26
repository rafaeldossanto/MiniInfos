using System.Globalization;
using MiniInfos.Providers;

namespace MiniInfos.Tests.Providers;

public sealed class PlanWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 15, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void AWindowWithAReadingCountsFromItsOwnStart()
    {
        var reset = Now.AddHours(2);
        var window = new PlanWindow(TimeSpan.FromHours(5), Limit(reset));

        Assert.Equal(reset.AddHours(-5), window.Start(Now));
    }

    [Fact]
    public void AWindowThatResetAlreadyCountsFromTheReset()
    {
        var reset = Now.AddMinutes(-10);

        Assert.Equal(reset, new PlanWindow(TimeSpan.FromHours(5), Limit(reset)).Start(Now));
    }

    [Fact]
    public void AWindowWithNoReadingCountsBackItsLength() =>
        Assert.Equal(Now.AddDays(-7), new PlanWindow(TimeSpan.FromDays(7), null).Start(Now));

    private static RateLimit Limit(DateTimeOffset reset) => new("primary", 50, reset.ToString("O", CultureInfo.InvariantCulture));
}
