using Avalonia.Threading;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Commands;
using ReiEditor.ViewModels.Windows.Editor.Console;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;
using ReiEditor.ViewModels.Windows.Editor.Monitor;
using ReiEditor.ViewModels.Windows.Editor.Playmode;
using ReiEditor.ViewModels.Windows.Editor.Project;
using ReiEditor.ViewModels.Windows.Editor.StatusBar;

namespace ReiEditor.ViewModels.Windows.Editor;

public class ProjectEditorWindowViewModel : BaseViewModel
{
    public SaveProjectCommand SaveProjectCommand { get; }
    public BuildProjectCommand BuildProjectCommand { get; }
    public ImportEngineResourcesCommand ImportEngineResourcesCommand { get; }
    public OpenSettingsWindowCommand OpenSettingsCommand { get; }
    public OpenBuildProjectWindowCommand OpenBuildProjectWindowCommand { get; }

    public PlaymodePanelViewModel PlaymodePanel { get; } = new();
    public ConsoleEditorWindowViewModel Console { get; } = new();
    public ConsoleEditorWindowHeaderViewModel ConsoleHeader { get; } = new();
    public StatusBarViewModel StatusBar { get; } = new();

    public HierarchyWindowViewModel Hierarchy { get; } = new();
    public MonitorWindowViewModel Monitor { get; } = new();
    public ProjectWindowViewModel ProjectWindow { get; } = new();

    private Scene? _activeScene;

    private readonly ISceneManagementService _sceneManagementService;

#pragma warning disable CS8618
    public ProjectEditorWindowViewModel()
    {
        ConsoleHeader = new ConsoleEditorWindowHeaderViewModel(Console);
    }
#pragma warning restore CS8618

    public ProjectEditorWindowViewModel(
        ISceneManagementService sceneManagementService,
        IFactory<PlaymodePanelViewModel> playmodePanel,
        IFactory<ConsoleEditorWindowViewModel> console,
        IFactory<BuildProjectCommand> buildProjectCommandFactory,
        IFactory<ImportEngineResourcesCommand> importEngineResourcesCommandFactory,
        IFactory<OpenSettingsWindowCommand> openSettingsCommandFactory,
        IFactory<OpenBuildProjectWindowCommand> openBuildProjectWindowCommandFactory,
        IFactory<StatusBarViewModel> statusBarViewModelFactory,
        IFactory<HierarchyWindowViewModel> hierarchyFactory,
        IFactory<SaveProjectCommand> saveProjectCommand,
        IFactory<MonitorWindowViewModel> monitorWindowFactory,
        IFactory<ProjectWindowViewModel> projectWindowFactory)
    {
        _sceneManagementService = sceneManagementService;
        SaveProjectCommand = saveProjectCommand.CreateInstance();
        BuildProjectCommand = buildProjectCommandFactory.CreateInstance();
        ImportEngineResourcesCommand = importEngineResourcesCommandFactory.CreateInstance();
        OpenSettingsCommand = openSettingsCommandFactory.CreateInstance();
        OpenBuildProjectWindowCommand = openBuildProjectWindowCommandFactory.CreateInstance();

        PlaymodePanel = playmodePanel.CreateInstance();
        Console = console.CreateInstance();
        ConsoleHeader = new ConsoleEditorWindowHeaderViewModel(Console);
        StatusBar = statusBarViewModelFactory.CreateInstance();
        Hierarchy = hierarchyFactory.CreateInstance(new Hierarchy<GameEntity>(""));
        Monitor = monitorWindowFactory.CreateInstance();
        ProjectWindow = projectWindowFactory.CreateInstance();
    }

    public override void Dispose()
    {
        base.Dispose();
        PlaymodePanel.Dispose();
        Console.Dispose();
        BuildProjectCommand.Dispose();
        OpenSettingsCommand.Dispose();
        OpenBuildProjectWindowCommand.Dispose();
        StatusBar.Dispose();
        Hierarchy.Dispose();
        ProjectWindow.Dispose();
        ConsoleHeader.Dispose();

        _sceneManagementService.CurrentScene.Unsubscribe(HandleCurrentSceneChangedEvent);
    }

    public void OnProjectLoaded()
    {
        _sceneManagementService.CurrentScene.Subscribe(HandleCurrentSceneChangedEvent);

        HandleCurrentSceneChangedEvent(_sceneManagementService.CurrentScene.Value);
    }

    private void HandleCurrentSceneChangedEvent(Scene? scene)
    {
        if (_activeScene != null)
        {
            _activeScene.HierarchyRebuiltEvent -= HandleSceneHierarchyRebuiltEvent;
        }

        _activeScene = scene;

        if (_activeScene != null)
        {
            _activeScene.HierarchyRebuiltEvent += HandleSceneHierarchyRebuiltEvent;
        }

        HandleSceneHierarchyRebuiltEvent();
    }

    private void HandleSceneHierarchyRebuiltEvent()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            Hierarchy.SetHierarchy(_activeScene == null ? new Hierarchy<GameEntity>("") : _activeScene.Hierarchy);
        });
    }
}
