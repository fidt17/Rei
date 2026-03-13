using System;
using System.Linq;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.ProjectManagement.Update;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Models.Services.Scenes.Templates;
using ReiEditor.Utils.Common.Procedures;
using ReiEditor.Utils.Path;

namespace ReiEditor.Models.ProjectManagement.Setup;

public class ProjectSetupService : IProjectSetupService
{
    private readonly ILogger<ProjectSetupService> _logger;
    private readonly ISceneManagementService _sceneManagementService;
    private readonly IActiveProjectService _activeProjectService;
    private readonly IAssetsService _assetsService;
    private readonly IEditorProceduresService _editorProceduresService;
    private readonly IProjectUpdateService _projectUpdateService;
    private readonly DefaultSceneTemplate _defaultSceneTemplate;
    private readonly IBuildStarter _buildStarter;
    private readonly IResourceService _resourceService;

    public ProjectSetupService(
        ILogger<ProjectSetupService> logger, 
        ISceneManagementService sceneManagementService, 
        IActiveProjectService activeProjectService, 
        IAssetsService assetsService, 
        IEditorProceduresService editorProceduresService, 
        IProjectUpdateService projectUpdateService, 
        DefaultSceneTemplate defaultSceneTemplate, 
        IBuildStarter buildStarter, 
        IResourceService resourceService)
    {
        _logger = logger;
        _sceneManagementService = sceneManagementService;
        _activeProjectService = activeProjectService;
        _assetsService = assetsService;
        _editorProceduresService = editorProceduresService;
        _projectUpdateService = projectUpdateService;
        _defaultSceneTemplate = defaultSceneTemplate;
        _buildStarter = buildStarter;
        _resourceService = resourceService;
    }

    public async Task PrepareProject()
    {
        var prepareProjectProcedure = new Procedure("Loading project");
        _editorProceduresService.TrackProcedure(prepareProjectProcedure);

        var project = _activeProjectService.GetActiveProject();
        
        await _projectUpdateService.UpdateProject(project);
        await _sceneManagementService.InitializeAsync();
		
        if (!project.HasBeenSetup)
        {
            await SetupNewProject();
            project.SetHasBeenSetup(true);
            await _assetsService.SaveProject();
        }
        else
        {
            await OpenLastScene();
        }

        await _buildStarter.BuildProject(BuildConfigurationEnum.EditorDebug);
		
        prepareProjectProcedure.Complete();
    }
	
    private async Task SetupNewProject()
    {
        _logger.Log("Setup new project");
		
        var defaultScene = await CreateAndLoadDefaultScene();
        if (defaultScene != null)
        {
            _sceneManagementService.SetBuildSceneId(defaultScene, 0);
            _activeProjectService.GetActiveProject().SetLastScene(defaultScene.AssetId);
        }
    }

    private async Task OpenLastScene()
    {
        var lastSceneId = _activeProjectService.GetActiveProject().LastSceneId;
        var lastScene = await _assetsService.Load<Scene>(lastSceneId);

        if (lastScene == null)
        {
            var sceneFromBuildConfig = _sceneManagementService.GetBuildConfiguration().Scenes.First().Value;
            lastScene = await _assetsService.Load<Scene>(sceneFromBuildConfig);

            if (lastScene == null)
            {
                _logger.LogWarning("Last scene is missing. Creating default one");
                await CreateAndLoadDefaultScene();
                return;
            }
        }

        await _sceneManagementService.LoadScene(lastScene ?? throw new NullReferenceException("last scene is missing"));
    }

    private async Task<Scene?> CreateAndLoadDefaultScene()
    {
        var sceneName = PathNamingUtils.GetUniqueAssetName(_resourceService.GetProjectPath("Scenes"), "New Scene", FileExtensions.SCENE);
        var scene = await _sceneManagementService.CreateScene(sceneName, "Scenes");
        
        if (scene == null)
        {
            _logger.LogError("Default scene creation failed");
        }
        else
        {
            await _sceneManagementService.LoadScene(scene);
            await _defaultSceneTemplate.SetupScene();
            _sceneManagementService.SetBuildSceneId(scene, 0);
        }

        return scene;
    }
}
