using Editor.Models.App.MainWindow;
using Editor.Models.Services.Logging;
using Editor.Utils.Factory;
using Editor.ViewModels.ProjectManagement;
using ProjectManagementWindow = Editor.Views.ProjectManagement.ProjectManagementWindow;

namespace Editor.Startup.EntryPoints;

public class ProjectManagementEntryPoint
{
	private readonly ILogger<ProjectManagementEntryPoint> _logger;
	private readonly IMainWindowService _mainWindowService;
	private readonly IFactory<ProjectManagementWindowViewModel> _projectManagementWindowViewModelFactory;
	
	public ProjectManagementEntryPoint(
		ILogger<ProjectManagementEntryPoint> logger,
		IMainWindowService _mainWindowService,
		IFactory<ProjectManagementWindowViewModel> projectManagementWindowViewModelFactory)
	{
		_logger = logger;
		this._mainWindowService = _mainWindowService;
		_projectManagementWindowViewModelFactory = projectManagementWindowViewModelFactory;
	}

	public void Start()
	{
		_logger.Log("Start");

		var window = new ProjectManagementWindow();
		var vm = _projectManagementWindowViewModelFactory.CreateInstance();
		window.DataContext = vm;
		_mainWindowService.ShowMainWindow(window);
		vm.OpenProjectsListTab();
	}
}