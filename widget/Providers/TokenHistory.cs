namespace MiniInfos.Providers;

/// <summary>Local tokens in eighteen twenty-minute intervals, oldest first. Zero means no recorded use.</summary>
public sealed record TokenHistory(DateTimeOffset Until, IReadOnlyList<long> Tokens)
{
    public const int BucketCount = 18;
    public static readonly TimeSpan BucketLength = TimeSpan.FromMinutes(20);
    public static readonly TimeSpan Length = TimeSpan.FromHours(6);
    public DateTimeOffset Start => Until - Length;
}
