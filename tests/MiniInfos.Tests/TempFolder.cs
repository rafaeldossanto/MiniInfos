namespace MiniInfos.Tests;

/// <summary>A folder of its own for one test, removed with it.</summary>
public sealed class TempFolder : IDisposable
{
    public TempFolder() => Directory.CreateDirectory(Path);

    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MiniInfos.Tests-" + Guid.NewGuid().ToString("N"));

    public string File(params string[] parts)
    {
        var path = System.IO.Path.Combine([Path, .. parts]);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
    }
}
