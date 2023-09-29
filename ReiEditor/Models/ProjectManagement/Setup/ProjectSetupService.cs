using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.ProjectManagement.Setup;

public class ProjectSetupService : IProjectSetupService
{
	private readonly ILogger<ProjectSetupService> _logger;
	private readonly ISceneManagementService _sceneManagementService;
	private readonly IActiveProjectService _activeProjectService;
	private readonly IAssetsService _assetsService;

	public ProjectSetupService(ILogger<ProjectSetupService> logger, ISceneManagementService sceneManagementService, IActiveProjectService activeProjectService, IAssetsService assetsService)
	{
		_logger = logger;
		_sceneManagementService = sceneManagementService;
		_activeProjectService = activeProjectService;
		_assetsService = assetsService;
	}

	public void AnalyzeProject()
	{
		var project = _activeProjectService.GetActiveProject();
		if (project.HasBeenSetup) return;
		
		_logger.Log("Initial project setup");
		project.SetHasBeenSetup(true);
		CreateTemplateScenes();

		_assetsService.SaveProject();
	}

	private void CreateTemplateScenes()
	{
		_sceneManagementService.CreateScene("New Scene", "Scenes");
	}
}