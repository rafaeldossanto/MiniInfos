using MiniInfos.Providers;

namespace MiniInfos.Tests.Providers;

public sealed class InstalledProvidersTests
{
    private readonly FakeProvider _claude = new("claude");
    private readonly FakeProvider _codex = new("codex");
    private readonly FakeProvider _third = new("third");

    [Fact]
    public void OnlyTheInstalledProvidersGetAPageInTheirOrder()
    {
        _codex.Installed = false;

        Assert.Equal([_claude, _third], new InstalledProviders([_claude, _codex, _third]).Shown);
    }

    [Fact]
    public void WithNoneInstalledEveryProviderShows()
    {
        _claude.Installed = _codex.Installed = false;

        Assert.Equal([_claude, _codex], new InstalledProviders([_claude, _codex]).Shown);
    }

    [Fact]
    public async Task AnAppRemovedTakesItsPageAwayAndOneInstalledBringsItBackInPlace()
    {
        var providers = new InstalledProviders([_claude, _codex, _third]);
        var changes = 0;
        providers.Changed += () => changes++;

        _codex.Installed = false;
        await providers.CheckAsync();
        Assert.Equal([_claude, _third], providers.Shown);

        _codex.Installed = true;
        await providers.CheckAsync();
        Assert.Equal([_claude, _codex, _third], providers.Shown);
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task ALookThatFindsTheSameAppsChangesNothing()
    {
        var providers = new InstalledProviders([_claude, _codex]);
        var changes = 0;
        providers.Changed += () => changes++;

        await providers.CheckAsync();

        Assert.Equal(0, changes);
    }

    private sealed class FakeProvider(string id) : IUsageProvider
    {
        public bool Installed { get; set; } = true;

        public string Id => id;

        public bool IsInstalled() => Installed;

        public bool IsInUse() => false;

        public Task<UsagePage> ReadAsync(DateTimeOffset now, bool isShown, bool force) => throw new NotSupportedException();

        public void Flush() { }
    }
}
