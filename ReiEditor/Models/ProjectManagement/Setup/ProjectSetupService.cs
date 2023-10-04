using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Utils.Common.Procedures;

namespace ReiEditor.Models.ProjectManagement.Setup;

public class ProjectSetupService : IProjectSetupService
{
	private readonly ILogger<ProjectSetupService> _logger;
	private readonly ISceneManagementService _sceneManagementService;
	private readonly IActiveProjectService _activeProjectService;
	private readonly IAssetsService _assetsService;
	private readonly IEditorProceduresService _editorProceduresService;

	public ProjectSetupService(
		ILogger<ProjectSetupService> logger, 
		ISceneManagementService sceneManagementService, 
		IActiveProjectService activeProjectService, 
		IAssetsService assetsService, 
		IEditorProceduresService editorProceduresService)
	{
		_logger = logger;
		_sceneManagementService = sceneManagementService;
		_activeProjectService = activeProjectService;
		_assetsService = assetsService;
		_editorProceduresService = editorProceduresService;
	}

	public async Task PrepareProject()
	{
		var prepareProjectProcedure = new Procedure("Loading project");
		_editorProceduresService.TrackProcedure(prepareProjectProcedure);

		var project = _activeProjectService.GetActiveProject();
		
		if (!project.HasBeenSetup)
		{
			project.SetHasBeenSetup(true);
			await SetupNewProject();
			return;
		}
		
		await OpenLastScene();
		
		prepareProjectProcedure.Complete();
	}
	
	private async Task SetupNewProject()
	{
		_logger.Log("Setup new project");
		
		var defaultScene = await CreateDefaultScene();
		if (defaultScene != null)
		{
			_sceneManagementService.SetBuildSceneId(defaultScene, 0);
			_activeProjectService.GetActiveProject().SetLastScene(defaultScene);
		}
		
		await _assetsService.SaveProject();
	}

	private async Task OpenLastScene()
	{
		var lastSceneId = _activeProjectService.GetActiveProject().LastSceneId;
		var lastScene = await _assetsService.Load<Scene>(lastSceneId);

		if (lastScene == null)
		{
			_logger.LogWarning("Last scene is missing. Creating default one");
			lastScene = await CreateDefaultScene();
			await _assetsService.SaveProject();
		}

		if (lastScene == null) return;
		await _sceneManagementService.LoadScene(lastScene);
	}

	private async Task<Scene?> CreateDefaultScene()
	{
		var scene = await _sceneManagementService.CreateScene("New Scene", "Scenes");
		if (scene == null)
		{
			_logger.LogError("Default scene creation failed");
		}

		return scene;
	}
}