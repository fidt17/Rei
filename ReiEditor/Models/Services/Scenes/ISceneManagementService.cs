using System.Threading.Tasks;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Scenes;

public interface ISceneManagementService
{
	IObservable<Scene?> CurrentScene { get; }

	Task<Scene?> CreateScene(string name, string projectPath);
	Task LoadScene(Scene scene);
}