using Editor.Models.Services.Logging;
using Editor.Utils.Factory;
using Editor.ViewModels;

namespace Editor.Startup;

public class EditorEntryPoint
{
	private readonly ILogger<EditorEntryPoint> _logger;
	private readonly MainWindowViewModel _mainWindow;
	private readonly IFactory<ProjectManagementWindowViewModel> _projectManagementWindowViewModelFactory;

	public EditorEntryPoint(
		ILogger<EditorEntryPoint> logger,
		MainWindowViewModel mainWindow,
		IFactory<ProjectManagementWindowViewModel> projectManagementWindowViewModelFactory)
	{
		_logger = logger;
		_mainWindow = mainWindow;
		_projectManagementWindowViewModelFactory = projectManagementWindowViewModelFactory;
	}

	public void Start()
	{
		_logger.LogWarn("Editor started");

		var projectManagementWindowViewModel = _projectManagementWindowViewModelFactory.CreateInstance();
		_mainWindow.TabContainer.OpenTab(projectManagementWindowViewModel);
		projectManagementWindowViewModel.OpenProjectsListTab();
	}
}