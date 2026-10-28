using MiniInfos.Providers.Claude;
using MiniInfos.Providers.Codex;
using MiniInfos.Providers.Gemini;
using MiniInfos.Providers.Kimi;

namespace MiniInfos.Providers;

public static class UsageProviders
{
    /// <summary>Every provider, in the order of the card's pages: a new one is one more entry here.</summary>
    public static IReadOnlyList<IUsageProvider> Create() => [new ClaudeUsage(), new CodexUsage(), new GeminiUsage(), new KimiUsage()];
}
