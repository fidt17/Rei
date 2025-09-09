using System.Threading.Tasks;
using ReiEditor.Startup.Common;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Scenes;

public interface ISceneManagementService : IAsyncInitializable
{
    IObservable<Scene?> CurrentScene { get; }

    Task<Scene?> CreateScene(string name, string projectPath);
    Task LoadScene(Scene scene);
    Task ReloadCurrentScene();
	
    BuildScenesConfiguration GetBuildConfiguration();
    void SetBuildSceneId(Scene scene, int id);
}