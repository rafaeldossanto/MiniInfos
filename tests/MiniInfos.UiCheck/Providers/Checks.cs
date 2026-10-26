namespace MiniInfos.UiCheck;

internal static partial class Program
{
    private static void CheckApps()
    {
        Check(ClaudeApp.IsOpen(), "the Claude app is seen open (it runs these checks)");
        var codex = CodexCli.AppServer();
        Console.WriteLine($"     codex: {codex?.FileName ?? "none"} | Codex app open: {CodexApp.IsOpen()}");
    }
}
