using Editor.Models.App.MainWindow;
using Editor.Models.Services.Logging;
using Editor.Utils.Factory;
using Editor.ViewModels.Editor;
using Editor.Views.Editor;

namespace Editor.Startup.EntryPoints;

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