namespace ReiEditor.Tests.Infrastructure.Fixtures;

public sealed class TemporaryDirectory : IDisposable
{
    private readonly string _parentPath = Path.GetFullPath(Path.GetTempPath());
    private readonly string _directoryName = $"ReiEditor.Tests-{Guid.NewGuid():N}";

    public string RootPath { get; }

    public TemporaryDirectory()
    {
        RootPath = Path.Combine(_parentPath, _directoryName);
        Directory.CreateDirectory(RootPath);
    }

    public string GetPath(params string[] segments)
    {
        var path = Path.GetFullPath(Path.Combine(RootPath, Path.Combine(segments)));
        var relativePath = Path.GetRelativePath(RootPath, path);
        if (Path.IsPathRooted(relativePath) || relativePath == ".." || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}"))
        {
            throw new ArgumentException("Test paths must stay inside the temporary directory.", nameof(segments));
        }

        return path;
    }

    public void Dispose()
    {
        if (!Directory.Exists(RootPath)) return;

        var expectedPath = Path.Combine(_parentPath, _directoryName);
        if (Path.GetFullPath(RootPath) != expectedPath || (File.GetAttributes(RootPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("Refusing to delete a directory not owned by this fixture.");
        }

        Directory.Delete(RootPath, recursive: true);
    }
}
