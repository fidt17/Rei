using ReiEditor.Models.Resources.Client;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

/// <summary>Delegates to isolated project resources while allowing writes to be recorded, gated or rejected explicitly.</summary>
internal sealed class TestResourceService(IResourceService inner) : IResourceService
{
    public List<(string Data, string Path)> Writes { get; } = new();
    public Func<string, string, Task<bool>>? OnWrite { get; set; }
    public string GetRootPath(params string[] path) => inner.GetRootPath(path);
    public string GetProjectPath(params string[] path) => inner.GetProjectPath(path);
    public string GetScriptsPath(params string[] path) => inner.GetScriptsPath(path);
    public IEnumerable<string> GetAllWithExtension(string extension) => inner.GetAllWithExtension(extension);
    public void CopyFilesRecursively(string source, string target) => inner.CopyFilesRecursively(source, target);
    public void MoveFilesRecursively(string source, string target) => inner.MoveFilesRecursively(source, target);
    public Task<T> Load<T>(string fullPath) => inner.Load<T>(fullPath);
    public Task<T?> TryLoad<T>(string fullPath) => inner.TryLoad<T>(fullPath);
    public bool Exists(string fullPath) => inner.Exists(fullPath);
    public Task<bool> Write(string data, string fullPath)
    {
        Writes.Add((data, fullPath));
        return OnWrite == null ? inner.Write(data, fullPath) : OnWrite(data, fullPath);
    }
}
