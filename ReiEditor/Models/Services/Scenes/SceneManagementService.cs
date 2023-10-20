using System;
using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Scenes;

public class SceneManagementService : ISceneManagementService
{
	public Utils.Common.IObservable<Scene?> CurrentScene => _currentScene;

	private readonly Observable<Scene?> _currentScene = new(null);
	private BuildScenesConfiguration? _buildScenesConfiguration;
	
	private readonly ILogger<SceneManagementService> _logger;
	private readonly IAssetsService _assets;
	private readonly IActiveProjectService _projectService;

	public SceneManagementService(ILogger<SceneManagementService> logger, IAssetsService assets, IActiveProjectService projectService)
	{
		_logger = logger;
		_assets = assets;
		_projectService = projectService;
	}

	public async Task InitializeAsync()
	{
		const string assetName = "Build Scenes Configuration";
		const string projectPath = $"Settings/Build/{assetName}{FileExtensions.ASSET}";
		_buildScenesConfiguration = await _assets.LoadFrom<BuildScenesConfiguration>(projectPath);
		
		if (_buildScenesConfiguration == null)
		{
			_buildScenesConfiguration = new BuildScenesConfiguration();
			await _assets.Create(_buildScenesConfiguration, SpecialAssetIds.BUILD_SCENES_CONFIGURATION, projectPath);
		}
	}

	public async Task<Scene?> CreateScene(string name, string projectPath)
	{
		try
		{
			var scene = new Scene(name);
			var didCreate = await _assets.Create(scene, projectPath + $"/{name}{FileExtensions.SCENE}");
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
		
		_projectService.GetActiveProject().SetLastScene(_assets.GetAssetId(scene));
		await _assets.SaveProject();

		_currentScene.Value = scene;
	}

	public BuildScenesConfiguration GetBuildConfiguration()
	{
		return _buildScenesConfiguration ?? throw new NullReferenceException("BuildScenesConfiguration is missing");
	}

	public void SetBuildSceneId(Scene scene, int id)
	{
		_logger.Log($"Set build scene id. [{scene.Name}] -> {id}");
		GetBuildConfiguration().Scenes[id] = _assets.GetAssetId(scene);
	}
}