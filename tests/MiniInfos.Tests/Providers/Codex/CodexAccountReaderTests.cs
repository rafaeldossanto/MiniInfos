using System.Text.Json;
using MiniInfos.Providers.Codex;
using MiniInfos.Providers;

namespace MiniInfos.Tests.Providers.Codex;

public sealed class CodexAccountReaderTests
{
    [Fact]
    public void TheCodexBucketGivesBothWindowsWithTheirLengthsAndResets()
    {
        var (primary, secondary) = Parse("""
            {"id":2,"result":{"rateLimitsByLimitId":{"codex":{
              "primary":{"usedPercent":86.5,"windowDurationMins":300,"resetsAt":1792886400},
              "secondary":{"usedPercent":13,"windowDurationMins":10080,"resetsAt":1793404800}}}}}
            """);

        Assert.Equal(TimeSpan.FromHours(5), primary!.Length);
        Assert.Equal(86.5, primary.Limit!.PercentUsed);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1792886400), primary.Limit.ResetsAtTime);
        Assert.Equal(TimeSpan.FromDays(7), secondary!.Length);
        Assert.Equal(13, secondary.Limit!.PercentUsed);
    }

    [Fact]
    public void TheOlderSingleBucketIsReadAndAMissingLengthFallsBackToThePlansUsual()
    {
        var (primary, secondary) = Parse("""{"id":2,"result":{"rateLimits":{"primary":{"usedPercent":40}}}}""");

        Assert.Equal(TimeSpan.FromHours(5), primary!.Length);
        Assert.Null(primary.Limit!.ResetsAtTime);
        Assert.Null(secondary);
    }

    [Theory]
    [InlineData("""{"id":2,"error":{"code":-32600,"message":"signed out"}}""")]
    [InlineData("""{"id":2,"result":{}}""")]
    [InlineData("""{"id":2,"result":{"rateLimits":{"primary":{"usedPercent":"lots"}}}}""")]
    public void AReplyWithoutLimitsGivesNone(string reply)
    {
        var (primary, secondary) = Parse(reply);

        Assert.Null(primary);
        Assert.Null(secondary);
    }

    private static (PlanWindow? Primary, PlanWindow? Secondary) Parse(string reply)
    {
        using var document = JsonDocument.Parse(reply);
        return CodexAccountReader.ParseLimits(document.RootElement);
    }
}
