using System;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Scenes;

public class SceneManagementService : ISceneManagementService, IDisposable
{
    public Utils.Common.IObservable<Scene?> CurrentScene => _currentScene;

    private readonly Observable<Scene?> _currentScene = new(null);
    private BuildScenesConfiguration? _buildScenesConfiguration;
	
    private readonly ILogger<SceneManagementService> _logger;
    private readonly IAssetsService _assets;
    private readonly IAssetCreator _assetCreator;
    private readonly IActiveProjectService _projectService;
    private readonly ISelectionService _selectionService;
    private readonly IBehaviourComponentsService _behaviourComponentsService;
    private readonly IPlaymodeService _playmodeService;

    public SceneManagementService(
        ILogger<SceneManagementService> logger,
        IAssetsService assets,
        IActiveProjectService projectService,
        IAssetCreator assetCreator,
        ISelectionService selectionService, 
        IBehaviourComponentsService behaviourComponentsService, 
        IPlaymodeService playmodeService)
    {
        _logger = logger;
        _assets = assets;
        _projectService = projectService;
        _assetCreator = assetCreator;
        _selectionService = selectionService;
        _behaviourComponentsService = behaviourComponentsService;
        _playmodeService = playmodeService;
        
        _playmodeService.IsPlaymodeActive.Subscribe(HandleIsPlaymodeActiveValueChanged);
    }

    public void Dispose()
    {
        _playmodeService.IsPlaymodeActive.Unsubscribe(HandleIsPlaymodeActiveValueChanged);
    }

    public async Task InitializeAsync()
    {
        const string ASSET_NAME = "Build Scenes Configuration";
        const string PROJECT_PATH = $"Settings/Build/{ASSET_NAME}{FileExtensions.ASSET}";
		
        _logger.Log("Initialize");
		
        _buildScenesConfiguration = await _assets.LoadFrom<BuildScenesConfiguration>(PROJECT_PATH);
		
        if (_buildScenesConfiguration == null)
        {
            _buildScenesConfiguration = new BuildScenesConfiguration();
            await _assetCreator.Create(_buildScenesConfiguration, SpecialAssetIds.BUILD_SCENES_CONFIGURATION, PROJECT_PATH);
        }
    }

    public async Task<Scene?> CreateScene(string name, string projectPath)
    {
        try
        {
            var scene = new Scene(name);
            var didCreate = await _assetCreator.Create(scene, projectPath + $"/{name}{FileExtensions.SCENE}");
            if (!didCreate) throw new Exception("Scene creation failed");

            return scene;
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }

        return null;
    }

    public Task LoadScene(Scene scene)
    {
        _logger.Log($"Loading scene name={scene.Name}, id={scene.AssetId}");
		
        foreach (var sceneEntity in scene.Entities)
        {
            _behaviourComponentsService.RefreshComponents(sceneEntity);
        }
        
        _projectService.GetActiveProject().SetLastScene(scene.AssetId);
        _currentScene.SetAndInvoke(scene);
        _selectionService.ResetSelection();
		
        return Task.CompletedTask;
    }

    public async Task ReloadCurrentScene()
    {
        if (_currentScene.Value == null)
        {
            _logger.LogError("Cannot reload scene because current scene is missing");
            return;
        }
		
        _assets.Unload(_currentScene.Value.AssetId);
        var scene = await _assets.Load<Scene>(_currentScene.Value.AssetId);
        
        if (scene == null)
        {
            _logger.LogError($"Could not load scene id={_currentScene.Value.AssetId}");
            return;
        }
        
        await LoadScene(scene);
    }

    public BuildScenesConfiguration GetBuildConfiguration()
    {
        return _buildScenesConfiguration ?? throw new NullReferenceException("BuildScenesConfiguration is missing");
    }

    public void SetBuildSceneId(Scene scene, int id)
    {
        _logger.Log($"Set build scene id. [{scene.Name}] -> {id}");
        GetBuildConfiguration().Scenes[id] = scene.AssetId;
    }

    private void HandleIsPlaymodeActiveValueChanged(bool isActive)
    {
        if (isActive) return;

        Task.Run(ReloadCurrentScene);
    }
}