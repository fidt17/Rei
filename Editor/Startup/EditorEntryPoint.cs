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
		_logger.LogWarning("Editor started");

		var projectManagementWindowViewModel = _mainWindow.ActiveTab.Navigate(_projectManagementWindowViewModelFactory);
		projectManagementWindowViewModel.OpenCreateProjectTab();
	}
}