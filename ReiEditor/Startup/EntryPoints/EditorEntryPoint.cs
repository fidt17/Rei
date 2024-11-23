using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.ProjectManagement.Setup;
using ReiEditor.Models.Services.Build;
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
    private readonly IBuildService _buildService;

    public EditorEntryPoint(
        ILogger<EditorEntryPoint> logger, 
        IMainWindowService mainWindowService, 
        IFactory<ProjectEditorWindowViewModel> projectEditorWindowViewModelFactory,
        IProjectSetupService projectSetupService, 
        IBuildService buildService)
    {
        _logger = logger;
        _mainWindowService = mainWindowService;
        _projectEditorWindowViewModelFactory = projectEditorWindowViewModelFactory;
        _projectSetupService = projectSetupService;
        _buildService = buildService;
    }

    public async Task Start()
    {
        _logger.LogWarning("\n--- Editor Entry Point ---\n");
        SetupEditorWindow();

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