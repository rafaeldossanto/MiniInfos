using System.IO;
using MiniInfos.Infrastructure.Desktop;
using MiniInfos.Infrastructure.Storage;

namespace MiniInfos.Providers.Claude;

/// <summary>
/// Registers the miniinfos mod in the user's Claude Code settings, which every copy of Claude Code on the account
/// reads, so any copy found will do. Each copy found is tried until one takes it, the one that did is remembered for
/// the next update and the uninstall, and only when none took it is the mod missing.
/// </summary>
public sealed class ClaudeMod(string rememberedPath, Func<IEnumerable<string>> find, Func<string, string, int> run)
{
    private const string Marketplace = "miniinfos";
    private const string Plugin = "miniinfos@miniinfos";
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(2);

    /// <summary>With the Claude Code that worked last kept in the app's data, found anywhere, and run for real.</summary>
    public static ClaudeMod Default { get; } = new(Path.Combine(AppPaths.Data, "claude-code.txt"), () => ClaudeCode.Find(),
        (claude, arguments) => CommandRunner.Run(claude, arguments, CommandTimeout));

    /// <param name="pluginFolder">The mod's marketplace as installed with the app.</param>
    /// <returns>Whether a Claude Code took the mod.</returns>
    public bool Install(string pluginFolder)
    {
        foreach (var claude in Candidates())
        {
            Unregister(claude);
            if (run(claude, $"plugin marketplace add \"{pluginFolder}\"") == 0 && run(claude, $"plugin install {Plugin} --scope user") == 0)
            {
                Remember(claude);
                return true;
            }
        }

        return false;
    }

    /// <summary>Takes the mod out of the user's settings with the first Claude Code that runs; none found, there is nothing to undo.</summary>
    public void Remove()
    {
        foreach (var claude in Candidates())
        {
            if (Unregister(claude)) return;
        }
    }

    /// <summary>The one that worked last, if it is still there, then every one found.</summary>
    private IEnumerable<string> Candidates()
    {
        var remembered = Remembered();
        if (remembered is not null) yield return remembered;
        foreach (var claude in find())
        {
            if (!string.Equals(claude, remembered, StringComparison.OrdinalIgnoreCase)) yield return claude;
        }
    }

    /// <returns>Whether this Claude Code ran at all: one that answered, even "not installed", can do the rest.</returns>
    private bool Unregister(string claude)
    {
        var ran = run(claude, $"plugin uninstall {Plugin} --scope user") >= 0;
        run(claude, $"plugin marketplace remove {Marketplace}");
        return ran;
    }

    private string? Remembered()
    {
        try
        {
            return File.ReadAllText(rememberedPath).Trim() is { Length: > 0 } path && File.Exists(path) && ClaudeCode.IsClaudeCode(path) ? path : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void Remember(string claude)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(rememberedPath)!);
            File.WriteAllText(rememberedPath, claude);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

}
