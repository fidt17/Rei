using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Commands;
using ReiEditor.ViewModels.Windows.Editor.Console;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;
using ReiEditor.ViewModels.Windows.Editor.Playmode;
using ReiEditor.ViewModels.Windows.Editor.StatusBar;

namespace ReiEditor.ViewModels.Windows.Editor;

public class ProjectEditorWindowViewModel : BaseViewModel
{
    public SaveProjectCommand SaveProjectCommand { get; }
    public BuildProjectCommand BuildProjectCommand { get; }
    public OpenSettingsWindowCommand OpenSettingsCommand { get; }
	
    public PlaymodePanelViewModel PlaymodePanel { get; } = new();
    public ConsoleEditorWindowViewModel Console { get; } = new();
    public StatusBarViewModel StatusBar { get; } = new();

    public ObservableField<HierarchyWindowViewModel> Hierarchy { get; } = new(new());

    private readonly ISceneManagementService _sceneManagementService;
    private readonly IFactory<HierarchyWindowViewModel> _hierarchyFactory;

#pragma warning disable CS8618
    public ProjectEditorWindowViewModel() { }
#pragma warning restore CS8618

    public ProjectEditorWindowViewModel(
        ISceneManagementService sceneManagementService,
        IFactory<PlaymodePanelViewModel> playmodePanel,
        IFactory<ConsoleEditorWindowViewModel> console,
        IFactory<BuildProjectCommand> buildProjectCommandFactory,
        IFactory<OpenSettingsWindowCommand> openSettingsCommandFactory,
        IFactory<StatusBarViewModel> statusBarViewModelFactory,
        IFactory<HierarchyWindowViewModel> hierarchyFactory,
        IFactory<SaveProjectCommand> saveProjectCommand)
    {
        _sceneManagementService = sceneManagementService;
        _hierarchyFactory = hierarchyFactory;
        SaveProjectCommand = saveProjectCommand.CreateInstance();
        BuildProjectCommand = buildProjectCommandFactory.CreateInstance();
        OpenSettingsCommand = openSettingsCommandFactory.CreateInstance();
		
        PlaymodePanel = playmodePanel.CreateInstance();
        Console = console.CreateInstance();
        StatusBar = statusBarViewModelFactory.CreateInstance();
        
        _sceneManagementService.CurrentScene.Subscribe(HandleCurrentSceneChangedEvent);
    }

    public override void Dispose()
    {
        base.Dispose();
        PlaymodePanel.Dispose();
        Console.Dispose();
        BuildProjectCommand.Dispose();
        OpenSettingsCommand.Dispose();
        StatusBar.Dispose();
        Hierarchy.Dispose();
        
        _sceneManagementService.CurrentScene.Unsubscribe(HandleCurrentSceneChangedEvent);
    }
    
    private void HandleCurrentSceneChangedEvent(Scene? scene)
    {
        Hierarchy.Value = _hierarchyFactory.CreateInstance(scene == null ? new Hierarchy<GameEntity>("") : scene.Hierarchy);
    }
}