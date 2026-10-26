namespace MiniInfos.UiCheck;

/// <summary>Deterministic data for UI checks: no session logs, CLI process or account request.</summary>
internal sealed class SampleProvider(string id, string title) : IUsageProvider
{
    public string Id => id;

    /// <summary>Whether its app counts as installed: a check sets it, then has the cards look again.</summary>
    public bool Installed { get; set; } = true;

    public bool IsInstalled() => Installed;
    public bool IsInUse() => true;
    public void Flush() { }

    public Task<UsagePage> ReadAsync(DateTimeOffset now, bool isShown, bool force)
    {
        var tokens = new TokenCount(1_000_000, 250_000, 500_000, 0);
        return Task.FromResult(new UsagePage(title,
            Line(TimeSpan.FromHours(5), 42), Line(TimeSpan.FromDays(7), 68),
            tokens, new TokenCount(32_000_000, 1_000_000, 0, 0), "Dados de teste",
            "Projeto de teste · contexto 42%", "Atividade de teste", "atualizado agora", "Atualizar dados de teste"));

        WindowLine Line(TimeSpan duration, double used) => new(
            new PlanWindow(duration, new RateLimit("test", used, now.Add(duration).ToString("O"))), tokens);
    }
}
