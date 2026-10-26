namespace MiniInfos.Providers;

/// <summary>
/// The providers that get a page: those whose app is installed on this PC, or every one when none is,
/// so the card still says where the numbers would come from. Looked at again as apps come and go.
/// </summary>
public sealed class InstalledProviders
{
    private readonly IReadOnlyList<IUsageProvider> _all;
    private Task? _check;

    /// <summary>Looks once right away, so the card opens with its pages already settled.</summary>
    public InstalledProviders(IReadOnlyList<IUsageProvider> all)
    {
        if (all.Count == 0) throw new ArgumentException("There must be a provider to show.", nameof(all));

        _all = all;
        Shown = Choose(all, Installed(all));
    }

    /// <summary>In the order of the full list.</summary>
    public IReadOnlyList<IUsageProvider> Shown { get; private set; }

    /// <summary><see cref="Shown"/> changed: raised on the thread that started the check.</summary>
    public event Action? Changed;

    /// <summary>The installed ones in their order, or all of them when none is.</summary>
    public static IReadOnlyList<IUsageProvider> Choose(IReadOnlyList<IUsageProvider> all, IReadOnlyCollection<IUsageProvider> installed) =>
        installed.Count == 0 ? all : all.Where(installed.Contains).ToArray();

    /// <summary>Looks again off the calling thread; a call while one runs waits for that one.</summary>
    public Task CheckAsync() => _check is { IsCompleted: false } running ? running : _check = CheckNowAsync();

    private async Task CheckNowAsync()
    {
        var next = Choose(_all, await Task.Run(() => Installed(_all)));
        if (next.SequenceEqual(Shown)) return;

        Shown = next;
        Changed?.Invoke();
    }

    private static IUsageProvider[] Installed(IReadOnlyList<IUsageProvider> all) => all.Where(provider => provider.IsInstalled()).ToArray();
}
