using System.IO;
using MiniInfos.Community;
using MiniInfos.Community.Catalog;

namespace MiniInfos.Catalog;

public static class CatalogCommand
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            if (args is ["validate", var folder])
            {
                var package = new CatalogValidator().Validate(folder);
                output.WriteLine($"Pacote válido: {package.Id} · v{package.Manifest.Version}.");
                return 0;
            }
            if (args.Length is 3 or 5 && args[0] == "build" && (args.Length == 3 || args[3] == "--previous"))
            {
                CatalogIndex? previous = null;
                if (args.Length == 5)
                {
                    if (new FileInfo(args[4]).Length > CatalogIndex.MaxBytes) throw new PackageException("O índice anterior excede 1 MB.");
                    previous = CatalogIndex.Read(File.ReadAllBytes(args[4]));
                }
                var index = new CatalogBuilder().Build(args[1], args[2], previous);
                output.WriteLine($"Catálogo gerado: {index.Packages.Length} pacote(s) em {Path.GetFullPath(args[2])}.");
                return 0;
            }
            error.WriteLine("Uso: validate <pasta-do-pacote> ou build <pasta-packages> <pasta-saida> [--previous <index.json>].");
            return 2;
        }
        catch (Exception e) when (e is PackageException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            error.WriteLine(e.Message);
            return 1;
        }
    }
}
