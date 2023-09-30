using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.ProjectManagement.Setup;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor;
using ProjectEditorWindow = ReiEditor.Views.Windows.Editor.ProjectEditorWindow;

namespace ReiEditor.Startup.EntryPoints;

public class EditorEntryPoint
{
	private readonly ILogger<EditorEntryPoint> _logger;
	private readonly IMainWindowService _mainWindowService;
	private readonly IFactory<ProjectEditorWindowViewModel> _projectEditorWindowViewModelFactory;
	private readonly IProjectSetupService _projectSetupService;
	private readonly IAssetsService _assetsService;

	public EditorEntryPoint(
		ILogger<EditorEntryPoint> logger, 
		IMainWindowService mainWindowService, 
		IFactory<ProjectEditorWindowViewModel> projectEditorWindowViewModelFactory,
		IProjectSetupService projectSetupService, IAssetsService assetsService)
	{
		_logger = logger;
		_mainWindowService = mainWindowService;
		_projectEditorWindowViewModelFactory = projectEditorWindowViewModelFactory;
		_projectSetupService = projectSetupService;
		_assetsService = assetsService;
	}

	public async Task Start()
	{
		_logger.Log("Start");
		SetupEditorWindow();
		await _assetsService.RefreshAssets();
		await _projectSetupService.PrepareProject();
	}

	private void SetupEditorWindow()
	{
		var window = new ProjectEditorWindow();
		var vm = _projectEditorWindowViewModelFactory.CreateInstance();
		window.DataContext = vm;
		
		_mainWindowService.ShowMainWindow(window);
	}
}