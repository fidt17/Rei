using ReiEditor.Models.App.MainWindow;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor;
using ProjectEditorWindow = ReiEditor.Views.Windows.Editor.ProjectEditorWindow;

namespace ReiEditor.Startup.EntryPoints;

public class EditorEntryPoint
{
	private readonly ILogger<EditorEntryPoint> _logger;
	private readonly IMainWindowService _mainWindowService;
	private readonly IFactory<ProjectEditorWindowViewModel> _projectEditorWindowViewModelFactory;

	public EditorEntryPoint(ILogger<EditorEntryPoint> logger, IMainWindowService mainWindowService, IFactory<ProjectEditorWindowViewModel> projectEditorWindowViewModelFactory)
	{
		_logger = logger;
		_mainWindowService = mainWindowService;
		_projectEditorWindowViewModelFactory = projectEditorWindowViewModelFactory;
	}

	public void Start()
	{
		_logger.Log("Start");
		SetupEditorWindow();
	}

	private void SetupEditorWindow()
	{
		var window = new ProjectEditorWindow();
		var vm = _projectEditorWindowViewModelFactory.CreateInstance();
		window.DataContext = vm;
		
		_mainWindowService.ShowMainWindow(window);
	}
}