using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Controls;
using ReiEditor.ViewModels.Windows.Editor.Commands.Entities;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies.Services;

namespace ReiEditor.ViewModels.Windows.Editor.Hierarchies;

public class HierarchyWindowViewModel : BaseViewModel
{
    public event Action<int>? ScrollToEntityRequested;

    public ICommand ResetSelectionCommand { get; }

    public ObservableField<string> SceneName { get; } = new("Scene Name");
    public ObservableCollection<HierarchyNodeViewModel> Nodes => _nodeCollectionController.Nodes;

    public ContextMenuViewModel RootContextMenu { get; } = new();

    private readonly ISelectionService _selectionService;
    private readonly CreateSceneEntityCommand _createSceneEntityCommand;
    private readonly ISelectedEntityActionService _selectedEntityActionService;
    private readonly HierarchyNodeCollectionController _nodeCollectionController;
    private readonly HierarchySelectionHandler _selectionHandler;
    private readonly HierarchyFocusController _focusController;

    private Hierarchy<GameEntity>? _activeHierarchy;

#pragma warning disable CS8618
    public HierarchyWindowViewModel() { }
#pragma warning restore CS8618

    public HierarchyWindowViewModel(
        Hierarchy<GameEntity> hierarchy,
        ISelectionService selectionService,
        IFactory<HierarchyNodeViewModel> hierarchyElementFactory,
        IFactory<CreateSceneEntityCommand> createSceneEntityCommand,
        ISelectedEntityActionService selectedEntityActionService)
    {
        _activeHierarchy = hierarchy;
        _selectionService = selectionService;
        _createSceneEntityCommand = createSceneEntityCommand.CreateInstance();
        _selectedEntityActionService = selectedEntityActionService;

        _nodeCollectionController = new HierarchyNodeCollectionController(hierarchyElementFactory, ConfigureNode);
        _selectionHandler = new HierarchySelectionHandler(
            selectionService,
            () => _nodeCollectionController.GetAllNodes().ToList(),
            GetVisibleNodes);
        _focusController = new HierarchyFocusController(
            entityId => _nodeCollectionController.FindByEntityId(entityId),
            _selectionHandler.ReplaceSelection,
            entityId => ScrollToEntityRequested?.Invoke(entityId));

        SetHierarchy(hierarchy);

        ResetSelectionCommand = ReactiveCommand.Create(_selectionHandler.ResetSelection);
        RootContextMenu.AddOption(new ContextMenuOption("New Entity", ExecuteCreateNewEntityContextMenu));
        _selectedEntityActionService.RenameEntityRequested += _focusController.HandleRenameEntityRequested;
        _selectionService.SelectionChanged.Subscribe(_selectionHandler.HandleSelectionChanged);
        _selectionService.ActiveSelection.Subscribe(_focusController.HandleActiveSelectionChanged);
    }

    public override void Dispose()
    {
        _createSceneEntityCommand.Dispose();
        _selectedEntityActionService.RenameEntityRequested -= _focusController.HandleRenameEntityRequested;
        _selectionService.SelectionChanged.Unsubscribe(_selectionHandler.HandleSelectionChanged);
        _selectionService.ActiveSelection.Unsubscribe(_focusController.HandleActiveSelectionChanged);
        _nodeCollectionController.Dispose();
    }

    public void SetHierarchy(Hierarchy<GameEntity> hierarchy)
    {
        _activeHierarchy = hierarchy;

        var expandedEntityIds = _nodeCollectionController.CaptureExpandedEntityIds();
        SceneName.Set(hierarchy.Name);
        _nodeCollectionController.SetHierarchy(hierarchy, expandedEntityIds);
        _selectionHandler.RestoreSelection();
        _focusController.HandleActiveSelectionChanged(_selectionService.ActiveSelection.Value);
    }

    private void ConfigureNode(HierarchyNodeViewModel node)
    {
        node.ConfigureSelectionActions(
            _selectionHandler.HandleNodeSelectionRequested,
            _selectionHandler.HandleNodeContextMenuSelectionRequested);
    }

    private void ExecuteCreateNewEntityContextMenu()
    {
        var entity = _createSceneEntityCommand.CreateEntity();
        if (entity == null) return;

        var node = _nodeCollectionController.GetAllNodes().FirstOrDefault(x => x.Node.Content == entity);
        if (node == null) return;

        _selectionHandler.ReplaceSelection(node);

        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            const int DELAY = 300;
            await Task.Delay(DELAY);

            node.StartRenameCommand.Execute(null);
        });
    }

    private IReadOnlyList<HierarchyNodeViewModel> GetVisibleNodes()
    {
        var visibleNodes = new List<HierarchyNodeViewModel>();
        foreach (var node in Nodes)
        {
            AppendVisibleNode(node, visibleNodes);
        }

        return visibleNodes;
    }

    private static void AppendVisibleNode(HierarchyNodeViewModel node, ICollection<HierarchyNodeViewModel> visibleNodes)
    {
        visibleNodes.Add(node);
        if (!node.Expanded.Value) return;

        foreach (var childNode in node.ChildNodes)
        {
            AppendVisibleNode(childNode, visibleNodes);
        }
    }
}
