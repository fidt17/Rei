using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

internal sealed class TestFileTrash(string directory) : IFileTrash
{
    public List<string[]> Batches { get; } = new();

    public void MoveToTrash(IReadOnlyList<string> paths)
    {
        Batches.Add(paths.ToArray());
        Directory.CreateDirectory(directory);
        foreach (var path in paths)
        {
            var destination = Path.Combine(directory, Path.GetFileName(path));
            if (Directory.Exists(path)) Directory.Move(path, destination);
            else File.Move(path, destination);
        }
    }
}
