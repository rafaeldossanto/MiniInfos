using MiniInfos.UI.Formatting;

namespace MiniInfos.Tests.UI.Formatting;

public sealed class FormatTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 15, 0, 0, TimeSpan.FromHours(-3));

    [Theory]
    [InlineData(999, "999")]
    [InlineData(51_200, "51,2k")]
    [InlineData(1_052_000, "1,1M")]
    [InlineData(3_747_374_621, "3,7B")]
    public void ShortCounts(long value, string text) => Assert.Equal(text, Format.Count(value));

    [Fact]
    public void WholeCountsGroupEveryThreeDigits() => Assert.Equal("1.412.345.678", Format.Full(1_412_345_678));

    [Theory]
    [InlineData(300, "Janela 5h", "últimas 5h")]
    [InlineData(10_080, "Semanal", "últimos 7 dias")]
    [InlineData(45, "Janela 45min", "últimos 45min")]
    [InlineData(1_440, "Janela 24h", "últimas 24h")]
    public void WindowsAreNamedByTheirLength(int minutes, string label, string span)
    {
        Assert.Equal(label, Format.WindowLabel(TimeSpan.FromMinutes(minutes)));
        Assert.Equal(span, Format.WindowSpan(TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void ResetsReadAsTheTimeLeft()
    {
        Assert.Equal("reinicia em 2h 13min", Format.Reset(Now.AddMinutes(133), Now));
        Assert.Equal("reinicia em 3d 4h", Format.Reset(Now.AddDays(3).AddHours(4), Now));
        Assert.Equal("reiniciou", Format.Reset(Now.AddMinutes(-1), Now));
    }
}
