using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.ProjectManagement.Setup;
using ReiEditor.Models.Services.Engine.Playmode;
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
    private readonly IEditorModeStarter _editorModeStarter;

    public EditorEntryPoint(
        ILogger<EditorEntryPoint> logger, 
        IMainWindowService mainWindowService, 
        IFactory<ProjectEditorWindowViewModel> projectEditorWindowViewModelFactory,
        IProjectSetupService projectSetupService, 
        IEditorModeStarter editorModeStarter)
    {
        _logger = logger;
        _mainWindowService = mainWindowService;
        _projectEditorWindowViewModelFactory = projectEditorWindowViewModelFactory;
        _projectSetupService = projectSetupService;
        _editorModeStarter = editorModeStarter;
    }

    public async Task Start()
    {
        _logger.LogWarning("\n--- Editor Entry Point ---\n");
        SetupEditorWindow();

        await _projectSetupService.PrepareProject();
        _editorModeStarter.Start();
    }

    private void SetupEditorWindow()
    {
        var window = new ProjectEditorWindow();
        var vm = _projectEditorWindowViewModelFactory.CreateInstance();
        window.DataContext = vm;
		
        _mainWindowService.ShowMainWindow(window);
    }
}