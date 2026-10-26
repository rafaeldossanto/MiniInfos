namespace MiniInfos.Catalog;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args) => CatalogCommand.Run(args, Console.Out, Console.Error);
}
