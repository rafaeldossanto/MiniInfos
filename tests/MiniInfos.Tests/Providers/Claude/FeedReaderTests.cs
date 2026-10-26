using System.Text.Json;
using MiniInfos.Providers.Claude;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Providers.Claude;

public sealed class FeedReaderTests : IDisposable
{
    private readonly TempFolder _folder = new();
    public void Dispose() => _folder.Dispose();

    [Fact]
    public void FeedsFromBothHomesSupplyTheLatestChatAndAccountLimits()
    {
        var now = DateTimeOffset.UtcNow;
        Write("old", now.AddMinutes(-1), 20);
        Write("moved", now, 40);

        var state = FeedReader.Read(now, [Path.Combine(_folder.Path, "old"), Path.Combine(_folder.Path, "moved")]);

        Assert.Equal("moved", state.Latest?.SessionId);
        Assert.Equal(40, state.Limits["five_hour"].PercentUsed);
        Assert.True(state.HasOpenChat);
    }

    private void Write(string folder, DateTimeOffset at, double used) =>
        File.WriteAllText(_folder.File(folder, "chat.json"), JsonSerializer.Serialize(new
        {
            sessionId = folder,
            updatedAt = at.ToUnixTimeMilliseconds(), answeredAt = at.ToUnixTimeMilliseconds(), aliveAt = at.ToUnixTimeMilliseconds(),
            rateLimits = new[] { new { kind = "five_hour", percentUsed = used, resetsAt = at.AddHours(5).ToString("O") } },
        }));
}
