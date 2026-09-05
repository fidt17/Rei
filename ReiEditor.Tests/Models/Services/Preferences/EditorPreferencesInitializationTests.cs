using ReiEditor.Models.Services.Preferences;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Preferences;

/// <summary>
/// Verifies asynchronous preference initialization without accessing user storage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Preferences")]
public sealed class EditorPreferencesInitializationTests
{
    /// <summary>
    /// Storage read failures propagate and do not replace existing data with defaults.
    /// </summary>
    [Fact]
    public async Task InitializePropagatesStorageReadFailureWithoutWriting()
    {
        var writes = 0;
        var storage = new TestEditorStorageService(
            _ => Task.FromException<string?>(new IOException("read failed")),
            (_, _) =>
            {
                writes++;
                return Task.FromResult(true);
            });
        IEditorPreferencesService service = new EditorPreferencesService(
            storage, new TestLogger<EditorPreferencesService>(), new JsonSerializer());

        var exception = await Assert.ThrowsAsync<IOException>(() => service.InitializeAsync());

        Assert.Equal("read failed", exception.Message);
        Assert.Equal(0, writes);
    }

    /// <summary>
    /// Initialization remains pending until storage supplies the saved preferences, then exposes their values.
    /// </summary>
    [Fact]
    public async Task InitializeWaitsForStorageBeforeExposingLoadedPreferences()
    {
        var requestedFile = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var storage = new TestEditorStorageService(fileName =>
        {
            requestedFile.SetResult(fileName);
            return read.Task;
        });
        IEditorPreferencesService service = new EditorPreferencesService(
            storage, new TestLogger<EditorPreferencesService>(), new JsonSerializer());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var initialization = service.InitializeAsync();
        try
        {
            Assert.Equal("preferences.json", await requestedFile.Task.WaitAsync(timeout.Token));
            Assert.False(initialization.IsCompleted);

            read.SetResult("""{"EnginePath":"fixture-engine","WindowContainerActiveTabs":{"left":"Hierarchy"}}""");
            await initialization.WaitAsync(timeout.Token);

            Assert.Equal("fixture-engine", service.GetEnginePath());
            Assert.Equal("Hierarchy", service.GetWindowContainerActiveTab("left"));
        }
        finally
        {
            read.TrySetResult("{}");
            await initialization.WaitAsync(timeout.Token);
        }
    }
}
