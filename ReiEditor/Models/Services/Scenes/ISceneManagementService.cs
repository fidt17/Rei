using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Scenes;

public interface ISceneManagementService
{
	Task<Scene?> CreateScene(string name, string projectPath);
	Task LoadScene(Scene scene);
}