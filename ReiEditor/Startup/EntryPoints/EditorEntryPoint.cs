using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.ProjectManagement.Setup;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Entities;
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
    private readonly SelectedEntityInputService _selectedEntityInputService;
    
    private ProjectEditorWindowViewModel? _projectEditorWindowViewModel;

    public EditorEntryPoint(
        ILogger<EditorEntryPoint> logger, 
        IMainWindowService mainWindowService, 
        IFactory<ProjectEditorWindowViewModel> projectEditorWindowViewModelFactory,
        IProjectSetupService projectSetupService, 
        IEditorModeStarter editorModeStarter,
        SelectedEntityInputService selectedEntityInputService)
    {
        _logger = logger;
        _mainWindowService = mainWindowService;
        _projectEditorWindowViewModelFactory = projectEditorWindowViewModelFactory;
        _projectSetupService = projectSetupService;
        _editorModeStarter = editorModeStarter;
        _selectedEntityInputService = selectedEntityInputService;
    }

    public async Task Start()
    {
        _logger.LogWarning("\n--- Editor Entry Point ---\n");
        SetupEditorWindow();

        await _projectSetupService.PrepareProject();
        _projectEditorWindowViewModel!.OnProjectLoaded();
        
        _editorModeStarter.Start();
    }

    private void SetupEditorWindow()
    {
        var window = new ProjectEditorWindow();
        _projectEditorWindowViewModel = _projectEditorWindowViewModelFactory.CreateInstance();
        window.DataContext = _projectEditorWindowViewModel;
		
        _mainWindowService.ShowMainWindow(window);
    }
}
