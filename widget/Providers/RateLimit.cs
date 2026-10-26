using System.Globalization;

namespace MiniInfos.Providers;

public sealed record RateLimit(string Kind, double PercentUsed, string? ResetsAt)
{
    public DateTimeOffset? ResetsAtTime =>
        DateTimeOffset.TryParse(ResetsAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time)
            ? time
            : null;
}
