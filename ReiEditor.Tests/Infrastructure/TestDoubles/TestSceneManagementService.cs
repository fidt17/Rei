using ReiEditor.Models.Services.Scenes;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

/// <summary>Supplies an observable current scene without loading assets or starting the editor.</summary>
internal sealed class TestSceneManagementService : ISceneManagementService
{
    public Observable<Scene?> Scene { get; } = new(null);
    public ReiEditor.Utils.Common.IObservable<Scene?> CurrentScene => Scene;
    public Task InitializeAsync() => throw new NotSupportedException();
    public Task<Scene?> CreateScene(string name, string projectPath) => throw new NotSupportedException();
    public Func<string, Task>? OnOpen { get; set; }
    public Task OpenScene(string assetId) => (OnOpen ?? throw new NotSupportedException())(assetId);
    public Task LoadScene(Scene scene) => throw new NotSupportedException();
    public Task ReloadCurrentScene() => throw new NotSupportedException();
    public Func<string, string, Scene?>? OnSynchronizeRuntimeScene { get; set; }
    public Scene? SynchronizeRuntimeScene(string assetId, string name) => OnSynchronizeRuntimeScene?.Invoke(assetId, name);
    public BuildScenesConfiguration GetBuildConfiguration() => throw new NotSupportedException();
    public void SetBuildSceneId(Scene scene, int id) => throw new NotSupportedException();
}
