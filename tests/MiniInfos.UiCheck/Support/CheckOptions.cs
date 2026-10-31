namespace MiniInfos.UiCheck;

internal sealed record CheckOptions(IReadOnlySet<string> Suites, string? Output, bool Quiet)
{
    private static readonly HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase)
        { "All", "Antigravity", "Cards", "Settings", "Themes", "Fonts", "Mascot", "Updates", "Providers", "Community", "Localization", "Conversations" };

    public bool Includes(string suite) => Suites.Contains("All") || Suites.Contains(suite);

    public static CheckOptions Parse(string[] args)
    {
        var suites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? output = null;
        var quiet = false;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--quiet": quiet = true; break;
                case "--suite":
                    if (++index >= args.Length) throw new ArgumentException("Informe um cenário após --suite.");
                    var names = args[index].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (names.Length == 0) throw new ArgumentException("Informe ao menos um cenário.");
                    foreach (var name in names)
                    {
                        if (!Known.Contains(name)) throw new ArgumentException($"Cenário desconhecido: {name}");
                        suites.Add(name);
                    }
                    break;
                default:
                    if (args[index].StartsWith('-') || output is not null) throw new ArgumentException($"Argumento desconhecido: {args[index]}");
                    output = System.IO.Path.GetFullPath(args[index]);
                    break;
            }
        }
        if (suites.Count == 0) suites.Add("All");
        return new(suites, output, quiet);
    }
}
