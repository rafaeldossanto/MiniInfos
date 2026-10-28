using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using MiniInfos.Infrastructure.Storage;

namespace MiniInfos.Infrastructure.Desktop;

/// <summary>
/// Whether an app is installed for this Windows user, by its <see cref="AppFootprint"/>. The cheap signs
/// come first; a running process, which lists every process, comes last.
/// </summary>
public static partial class InstalledApps
{
    private const int ErrorSuccess = 0;
    private const int ErrorInsufficientBuffer = 122;

    /// <summary>Where VS Code and its forks keep extensions, under the user's profile.</summary>
    private static readonly string[] EditorFolders = [".vscode", ".vscode-insiders", ".vscode-oss", ".cursor", ".windsurf"];

    public static bool Has(AppFootprint app, InstallContext context) =>
        app.StorePackages.Any(context.HasStorePackage)
        || app.Commands.Any(command => CommandPath.Find(context.Path, command) is not null)
        || app.Files.Any(File.Exists)
        || app.Bundles.Any(Directory.Exists)
        || app.EditorExtensions.Any(extension => HasEditorExtension(context.UserProfile, extension))
        || app.Processes.Any(context.IsRunning);

    /// <summary>Whether a Store package of this family is registered to the user: uninstalling takes it away.</summary>
    public static bool HasStorePackage(string familyName)
    {
        if (!OperatingSystem.IsWindows()) return false;
        // Asked with no room for the names, Windows answers how many there are.
        uint count = 0, bufferLength = 0;
        var result = GetPackagesByPackageFamily(familyName, ref count, IntPtr.Zero, ref bufferLength, IntPtr.Zero);
        return result is ErrorSuccess or ErrorInsufficientBuffer && count > 0;
    }

    /// <summary>
    /// An extension's folder (publisher.name-version, the platform maybe after it) in any editor. VS Code
    /// deletes an uninstalled one's folder later and lists it in .obsolete meanwhile: that one no longer counts.
    /// </summary>
    private static bool HasEditorExtension(string userProfile, string extension) => EditorFolders.Any(editor =>
    {
        var folder = Path.Combine(userProfile, editor, "extensions");
        var found = SourceFiles.Directories(folder, extension + "-*");
        if (found.Length == 0) return false;

        var removed = Obsolete(folder);
        return found.Any(path => !removed.Contains(Path.GetFileName(path)));
    });

    private static HashSet<string> Obsolete(string extensionsFolder)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(extensionsFolder, ".obsolete")));
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.EnumerateObject().Select(entry => entry.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // No list: nothing is waiting to be deleted.
            return [];
        }
    }

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetPackagesByPackageFamily(string packageFamilyName, ref uint count, IntPtr packageFullNames, ref uint bufferLength, IntPtr buffer);
}
