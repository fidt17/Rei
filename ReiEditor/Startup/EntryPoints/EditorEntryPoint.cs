using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.ProjectManagement.Setup;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Scenes;
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
    private readonly IAssetImporter _assetImporter;
    private readonly IBuildService _buildService;
    private readonly ISceneManagementService _sceneManagementService;

    public EditorEntryPoint(
        ILogger<EditorEntryPoint> logger, 
        IMainWindowService mainWindowService, 
        IFactory<ProjectEditorWindowViewModel> projectEditorWindowViewModelFactory,
        IProjectSetupService projectSetupService, 
        IBuildService buildService, 
        ISceneManagementService sceneManagementService, 
        IAssetImporter assetImporter)
    {
        _logger = logger;
        _mainWindowService = mainWindowService;
        _projectEditorWindowViewModelFactory = projectEditorWindowViewModelFactory;
        _projectSetupService = projectSetupService;
        _buildService = buildService;
        _sceneManagementService = sceneManagementService;
        _assetImporter = assetImporter;
    }

    public async Task Start()
    {
        _logger.LogWarning("\n--- Editor Entry Point ---\n");
        SetupEditorWindow();

        await _assetImporter.ReimportAll();
        await _sceneManagementService.InitializeAsync();
		
        await _projectSetupService.PrepareProject();
        await _buildService.BuildProject(BuildConfigurationEnum.EditorDebug);
    }

    private void SetupEditorWindow()
    {
        var window = new ProjectEditorWindow();
        var vm = _projectEditorWindowViewModelFactory.CreateInstance();
        window.DataContext = vm;
		
        _mainWindowService.ShowMainWindow(window);
    }
}