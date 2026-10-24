using System;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Common.Procedures;

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
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IEngineRunner _engineRunner;
    private readonly IEngineSceneLoader _sceneLoader;
    private readonly IEditorProceduresService _procedures;
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    public SceneManagementService(
        ILogger<SceneManagementService> logger,
        IAssetsService assets,
        IActiveProjectService projectService,
        IAssetCreator assetCreator,
        ISelectionService selectionService, 
        IBehaviourComponentsService behaviourComponentsService,
        IBehaviourRegistry behaviourRegistry,
        IEngineRunner engineRunner,
        IEngineSceneLoader sceneLoader,
        IEditorProceduresService procedures)
    {
        _logger = logger;
        _assets = assets;
        _projectService = projectService;
        _assetCreator = assetCreator;
        _selectionService = selectionService;
        _behaviourComponentsService = behaviourComponentsService;
        _behaviourRegistry = behaviourRegistry;
        _engineRunner = engineRunner;
        _sceneLoader = sceneLoader;
        _procedures = procedures;

        _engineRunner.IsActive.Subscribe(HandleEngineIsRunningValueChangedEvent, invoke: false);
    }

    public void Dispose()
    {
        _engineRunner.IsActive.Unsubscribe(HandleEngineIsRunningValueChangedEvent);
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

    public Task OpenScene(string assetId) => LoadSceneCore(() => _assets.Load<Scene>(assetId));

    public Task LoadScene(Scene scene) => LoadSceneCore(() => Task.FromResult<Scene?>(scene));

    private async Task LoadSceneCore(Func<Task<Scene?>> readScene)
    {
        await _loadGate.WaitAsync();
        var procedure = new Procedure(ProcedureTags.LOADING_SCENE);
        try
        {
            _procedures.TrackProcedure(procedure);
            procedure.ReportProgress(0);
            var scene = await readScene();
            if (scene == null) throw new InvalidOperationException("Could not read scene asset.");
            _logger.Log($"Loading scene name={scene.Name}, id={scene.AssetId}");
            foreach (var sceneEntity in scene.Entities)
                _behaviourComponentsService.RefreshComponents(sceneEntity);
            SceneTransformDataApplier.Apply(scene, _behaviourRegistry);
            scene.RebuildHierarchy();
            scene.NormalizeTransformOrders();

            if (_engineRunner.IsActive.Value)
            {
                _currentScene.SetAndInvoke(null);
                _selectionService.ResetSelection();
                if (_engineRunner.IsActive.Value && !await _sceneLoader.LoadSceneAsync(scene, percent => procedure.ReportProgress(percent / 100d)))
                    throw new InvalidOperationException($"Could not load scene {scene?.AssetId} in engine.");
            }
            _projectService.GetActiveProject().SetLastScene(scene.AssetId);
            _currentScene.SetAndInvoke(scene);
            _selectionService.ResetSelection();
            procedure.ReportProgress(1);
        }
        catch
        {
            _currentScene.SetAndInvoke(null);
            _selectionService.ResetSelection();
            if (_engineRunner.IsActive.Value) await _sceneLoader.LoadSceneAsync(null);
            throw;
        }
        finally
        {
            procedure.Complete();
            _loadGate.Release();
        }
    }

    public Scene? SynchronizeRuntimeScene(string assetId, string name)
    {
        if (_engineRunner.ActiveMode != EngineRunMode.PlayMode || _loadGate.CurrentCount == 0) return null;
        if (_currentScene.Value?.AssetId == assetId) return _currentScene.Value;
        _selectionService.ResetSelection(sendToEngine: false);
        Scene? scene = null;
        if (!string.IsNullOrEmpty(assetId))
        {
            scene = new Scene(name);
            scene.SetAssetInfo(new AssetInfo(new AssetMeta(assetId), ""));
        }
        _currentScene.SetAndInvoke(scene);
        return scene;
    }

    public Task ReloadCurrentScene()
    {
        if (string.IsNullOrEmpty(_projectService.GetActiveProject().LastSceneId))
        {
            _logger.LogError("Cannot reload scene because current scene is missing");
            return Task.CompletedTask;
        }
        return LoadSceneCore(async () =>
        {
            var assetId = _projectService.GetActiveProject().LastSceneId;
            _assets.Unload(assetId);
            return await _assets.Load<Scene>(assetId);
        });
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

    private void HandleEngineIsRunningValueChangedEvent(bool isRunning)
    {
        if (isRunning) return;
        if (_engineRunner.ActiveMode != EngineRunMode.PlayMode) return;

        _ = Task.Run(async () =>
        {
            try { await ReloadCurrentScene(); }
            catch (Exception error) { _logger.LogException(error); }
        });
    }
}
