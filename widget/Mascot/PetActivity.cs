using System.Text.Json.Serialization;
using MiniInfos.Providers;

namespace MiniInfos.Mascot;

[JsonConverter(typeof(JsonStringEnumConverter<PetSizeChoice>))]
public enum PetSizeChoice { Small, Medium, Large }

public enum PetState
{
    Idle, Active, Alert, Sleep,
    ProtectEyes, HeldHead, HeldLeftWing, HeldRightWing, HeldFeet, HeldBody,
}

/// <summary>Only observable usage, without inferring whether the agent is thinking or has finished.</summary>
public sealed record PetUsage(string Id, long Total, double? LimitPercent)
{
    public static PetUsage Of(string id, UsagePage page, DateTimeOffset now)
    {
        var limits = new[] { page.ShortWindow.Window.Limit, page.LongWindow.Window.Limit }
            .Where(limit => limit is not null && (limit.ResetsAtTime is not { } reset || reset > now))
            .Select(limit => (double?)limit!.PercentUsed);
        return new(id, page.Total.Total, limits.DefaultIfEmpty().Max());
    }
}

/// <summary>One activity model for every animation surface, driven by the reads MiniInfos already makes.</summary>
public sealed class PetActivity
{
    public static readonly TimeSpan ActiveFor = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan SleepAfter = TimeSpan.FromMinutes(2);
    private readonly Dictionary<string, long> _totals = [];
    private DateTimeOffset? _lastActivity;
    private bool _nearLimit;
    private bool _hasActivity;

    public void Observe(IReadOnlyList<PetUsage> usage, DateTimeOffset now)
    {
        _lastActivity ??= now;
        _nearLimit = usage.Any(sample => sample.LimitPercent >= 90);
        foreach (var sample in usage)
        {
            if (_totals.TryGetValue(sample.Id, out var previous) && sample.Total > previous) React(now);
            _totals[sample.Id] = sample.Total;
        }
    }

    public PetState StateAt(DateTimeOffset now)
    {
        if (_nearLimit) return PetState.Alert;
        if (_lastActivity is not { } last) return PetState.Idle;
        if (now - last >= SleepAfter) return PetState.Sleep;
        return _hasActivity && now - last < ActiveFor ? PetState.Active : PetState.Idle;
    }

    private void React(DateTimeOffset now)
    {
        _lastActivity = now;
        _hasActivity = true;
    }
}
