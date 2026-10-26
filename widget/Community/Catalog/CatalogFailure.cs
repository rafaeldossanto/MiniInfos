using System.IO;
using System.Net.Http;

namespace MiniInfos.Community.Catalog;

/// <summary>
/// What reading the catalog, a preview or a package can fail with, each shown as a message. A download that
/// fails its size or SHA-256 check throws InvalidDataException, which is no IOException: left out, it would close the app.
/// </summary>
public static class CatalogFailure
{
    public static bool IsExpected(Exception e) => e is PackageException or IOException or UnauthorizedAccessException
        or HttpRequestException or InvalidDataException;
}
