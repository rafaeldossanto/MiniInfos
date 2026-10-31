using System.Text.Json;
using System.Net.Http;
using MiniInfos.Providers.Antigravity;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Providers.Antigravity;

public sealed class AntigravityQuotaTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SharedGroupsAndArgonsIndependentWeeklyQuotaRemainSeparate()
    {
        var buckets = AntigravityQuotas.Parse(Summary());
        Assert.Equal(5, buckets.Count);
        var argon = Assert.Single(buckets, bucket => bucket.Group == "Gemini 4 Argon");
        Assert.Equal(TimeSpan.FromDays(7), argon.Length);
        Assert.Equal(42, argon.Limit.PercentUsed, 8);
        Assert.Equal(0, buckets.First(bucket => bucket.Group == "Gemini Models").Limit.PercentUsed);
    }

    [Fact]
    public async Task PageShowsEveryGroupWithoutInventingAnArgonFiveHourLimitOrCountingTokensPerGroup()
    {
        using var folder = new TempFolder();
        var provider = new AntigravityUsage(() => [], folder.File("ledger.json"), _ => Task.FromResult<string?>(Summary()));
        var page = await provider.ReadAsync(Now, true, false);
        Assert.NotNull(page.QuotaGroups);
        Assert.Equal(3, page.QuotaGroups.Count);
        var argon = page.QuotaGroups[0];
        Assert.Equal("Gemini 4 Argon", argon.Title);
        Assert.Null(argon.Models);
        Assert.Single(argon.Windows);
        Assert.Equal(TimeSpan.FromDays(7), argon.Windows[0].Length);
        Assert.Equal(new[] { TimeSpan.FromHours(5), TimeSpan.FromDays(7) }, page.QuotaGroups[1].Windows.Select(window => window.Length));
        Assert.Equal("", page.Activity);
        Assert.Equal(0, page.Total.Total);
    }

    [Fact]
    public async Task HiddenPagesDoNotQueryAndConcurrentRefreshesShareTheMinuteCache()
    {
        var calls = 0;
        var quotas = new AntigravityQuotas(async _ => { calls++; await Task.Delay(10); return Summary(); });
        Assert.Null(await quotas.ReadAsync(Now, false, false));
        Assert.Equal(0, calls);
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => quotas.ReadAsync(Now, true, false)));
        Assert.Equal(1, calls);
        await quotas.ReadAsync(Now.AddSeconds(30), true, false);
        Assert.Equal(1, calls);
        await quotas.ReadAsync(Now.AddSeconds(31), true, true);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task TemporaryFailuresKeepRecentReadingsButExpireInsteadOfDisplayingFalseZeroes()
    {
        var fail = false;
        var quotas = new AntigravityQuotas(_ => fail ? throw new HttpRequestException("offline") : Task.FromResult<string?>(Summary()));
        var first = await quotas.ReadAsync(Now, true, false);
        Assert.NotNull(first);
        fail = true;
        Assert.Same(first, await quotas.ReadAsync(Now.AddMinutes(1), true, false));
        Assert.Null(await quotas.ReadAsync(Now.AddMinutes(5), true, false));
    }

    [Fact]
    public void OmittedProtoZeroMeansExhaustedAndDailyWindowsUseTheirRealLength()
    {
        var text = """{"response":{"groups":[{"displayName":"Daily model","buckets":[{"window":"daily","resetTime":"2026-10-31T01:00:00Z"}]}]}}""";
        var bucket = Assert.Single(AntigravityQuotas.Parse(text));
        Assert.Equal(100, bucket.Limit.PercentUsed);
        Assert.Equal(TimeSpan.FromDays(1), bucket.Length);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"response\":null}")]
    [InlineData("{\"response\":{\"groups\":[null,3,{\"displayName\":\"Bad\",\"buckets\":[null,{\"window\":\"weekly\",\"remainingFraction\":-1,\"resetTime\":\"2026-11-06T01:00:00Z\"}]}]}}")]
    public void UnexpectedOrInvalidQuotaValuesDoNotBecomePercentages(string json) => Assert.Empty(AntigravityQuotas.Parse(json));

    /// <param name="at">The clock the summary is read with: the fixed test time, or the real one for a card that renders now.</param>
    internal static string Summary(DateTimeOffset? at = null)
    {
        var resets = (at ?? Now).AddDays(7).ToString("O");
        object Bucket(string window, double left) => new { window, remainingFraction = left, resetTime = resets };
        return JsonSerializer.Serialize(new
        {
            response = new { groups = new[]
            {
                new { displayName = "Gemini Models", buckets = new[] { Bucket("weekly", 1), Bucket("5h", 1) } },
                new { displayName = "Claude and GPT models", buckets = new[] { Bucket("weekly", 1), Bucket("5h", 1) } },
                new { displayName = "Gemini 4 Argon", buckets = new[] { Bucket("weekly", .58) } },
            } },
        });
    }
}
