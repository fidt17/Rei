using ReiEditor.Models.App.MainWindow;
using ReiEditor.Models.ProjectManagement.EditorSetup;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.ProjectManagement;
using ProjectManagementWindow = ReiEditor.Views.Windows.ProjectManagement.ProjectManagementWindow;

namespace ReiEditor.Startup.EntryPoints;

public class ProjectManagementEntryPoint
{
	private readonly ILogger<ProjectManagementEntryPoint> _logger;
	private readonly IMainWindowService _mainWindowService;
	private readonly IFactory<ProjectManagementWindowViewModel> _projectManagementWindowViewModelFactory;
	private readonly IEditorConfigurationService _editorConfigurationService;

	public ProjectManagementEntryPoint(
		ILogger<ProjectManagementEntryPoint> logger,
		IMainWindowService _mainWindowService,
		IFactory<ProjectManagementWindowViewModel> projectManagementWindowViewModelFactory,
		IEditorConfigurationService editorConfigurationService)
	{
		_logger = logger;
		this._mainWindowService = _mainWindowService;
		_projectManagementWindowViewModelFactory = projectManagementWindowViewModelFactory;
		_editorConfigurationService = editorConfigurationService;
	}

	public void Start()
	{
		_logger.Log("Start");

		var window = new ProjectManagementWindow();
		var vm = _projectManagementWindowViewModelFactory.CreateInstance();
		window.DataContext = vm;
		_mainWindowService.ShowMainWindow(window);

		if (_editorConfigurationService.IsEditorConfigurationValid())
		{
			vm.OpenProjectsListTab();
		}
		else
		{
			_logger.LogWarning("Editor configuration is invalid");
			vm.OpenEditorSetupTab();
		}
	}
}