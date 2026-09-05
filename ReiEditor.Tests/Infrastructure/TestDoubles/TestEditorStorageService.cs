using ReiEditor.Models.EditorApp.Storage;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

public sealed class TestEditorStorageService(Func<string, Task<string?>> read) : IEditorStorageService
{
    public Task<string?> ReadFromFile(string fileName) => read(fileName);

    public Task<bool> WriteToFile(string fileName, string value)
        => throw new InvalidOperationException($"Unexpected storage write to {fileName}.");
}
