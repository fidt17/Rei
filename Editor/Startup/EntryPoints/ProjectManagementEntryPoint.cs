using Editor.Models.Services.App.MainWindow;
using Editor.Models.Services.Logging;
using Editor.Utils.Factory;
using Editor.ViewModels;
using Editor.Views;

namespace Editor.Startup.EntryPoints;

public class ProjectManagementEntryPoint
{
	private readonly ILogger<ProjectManagementEntryPoint> _logger;
	private readonly IMainWindowService _mainWindowService;
	private readonly IFactory<ShellWindowViewModel> _shellWindowFactory;
	private readonly IFactory<ProjectManagementWindowViewModel> _projectManagementWindowViewModelFactory;
	
	public ProjectManagementEntryPoint(
		ILogger<ProjectManagementEntryPoint> logger,
		IMainWindowService _mainWindowService,
		IFactory<ShellWindowViewModel> _shellWindowFactory,
		IFactory<ProjectManagementWindowViewModel> projectManagementWindowViewModelFactory)
	{
		_logger = logger;
		this._mainWindowService = _mainWindowService;
		this._shellWindowFactory = _shellWindowFactory;
		_projectManagementWindowViewModelFactory = projectManagementWindowViewModelFactory;
	}

	public void Start()
	{
		_logger.Log("Start");

		var window = new ShellWindow();
		var vm = _shellWindowFactory.CreateInstance();
		window.DataContext = vm;
		_mainWindowService.ShowMainWindow(window);

		var projectManagementWindow = vm.ActiveTab.Navigate(_projectManagementWindowViewModelFactory);
		projectManagementWindow.OpenProjectsListTab();
	}
}