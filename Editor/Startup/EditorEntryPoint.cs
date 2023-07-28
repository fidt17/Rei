using Editor.Models.Services.Logging;
using Editor.Utils.Factory;
using Editor.ViewModels;

namespace Editor.Startup;

public class EditorEntryPoint
{
	private readonly ILogger<EditorEntryPoint> _logger;
	private readonly MainWindowViewModel _mainWindow;
	private readonly IFactory<ProjectSelectionWindowViewModel> _projectSelectionWindowViewModelFactory;

	public EditorEntryPoint(
		ILogger<EditorEntryPoint> logger,
		MainWindowViewModel mainWindow,
		IFactory<ProjectSelectionWindowViewModel> projectSelectionWindowViewModelFactory)
	{
		_logger = logger;
		_mainWindow = mainWindow;
		_projectSelectionWindowViewModelFactory = projectSelectionWindowViewModelFactory;
	}

	public void Start()
	{
		_logger.LogWarn("Editor started");
		
		_mainWindow.OpenProjectSelectionWindow(_projectSelectionWindowViewModelFactory);
	}
}