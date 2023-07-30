using Editor.Models.Services.Logging;
using Editor.Utils.Factory;
using Editor.ViewModels;

namespace Editor.Startup;

public class EditorEntryPoint
{
	private readonly ILogger<EditorEntryPoint> _logger;
	private readonly MainWindowViewModel _mainWindow;
	private readonly IFactory<ProjectManagementTabViewModel> _projectSelectionWindowViewModelFactory;

	public EditorEntryPoint(
		ILogger<EditorEntryPoint> logger,
		MainWindowViewModel mainWindow,
		IFactory<ProjectManagementTabViewModel> projectSelectionWindowViewModelFactory)
	{
		_logger = logger;
		_mainWindow = mainWindow;
		_projectSelectionWindowViewModelFactory = projectSelectionWindowViewModelFactory;
	}

	public void Start()
	{
		_logger.LogWarn("Editor started");
		
		_mainWindow.TabContainer.OpenTab(_projectSelectionWindowViewModelFactory.CreateInstance());
	}
}