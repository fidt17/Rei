using System;
using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Scenes;

public class SceneManagementService : ISceneManagementService
{
	private readonly ILogger<SceneManagementService> _logger;
	private readonly IAssetsService _assets;
	private readonly IActiveProjectService _projectService;

	public SceneManagementService(ILogger<SceneManagementService> logger, IAssetsService assets, IActiveProjectService projectService)
	{
		_logger = logger;
		_assets = assets;
		_projectService = projectService;
	}

	public async Task<Scene?> CreateScene(string name, string projectPath)
	{
		try
		{
			var scene = new Scene(_assets.AllocateAssetId(), name);
			var didCreate = await _assets.Create(scene, projectPath);
			if (!didCreate) throw new Exception("Scene creation failed");

			return scene;
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}

		return null;
	}

	public async Task LoadScene(Scene scene)
	{
		_logger.Log($"Loading scene [{scene.Name}]");
		
		_projectService.GetActiveProject().SetLastScene(scene);
		await _assets.SaveProject();
	}
}