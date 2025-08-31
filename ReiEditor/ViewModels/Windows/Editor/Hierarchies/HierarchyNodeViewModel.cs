using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ReactiveUI;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Utils;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Controls;

namespace ReiEditor.ViewModels.Windows.Editor.Hierarchies;

public class HierarchyNodeViewModel : BaseViewModel, IEntitySelectable
{
    public ICommand SelectCommand { get; }
    public RelayCommand StartRenameCommand { get; } = new();
    public ICommand ConfirmRenameCommand { get; }
    public ICommand DeleteCommand { get; }
    public MoveNodeCommand MoveNodeCommand { get; }

    GameEntity IEntitySelectable.Entity => Node.Content;
    
    public ObservableField<string> Name { get; } = new("Node");
    public ObservableField<string> RenameValue { get; } = new("");
	
    public ObservableField<bool> Selected { get; } = new(false);
    public ObservableField<bool> Expanded { get; } = new(false);

    public ObservableCollection<HierarchyNodeViewModel> ChildNodes { get; } = new();
    public ContextMenuViewModel ContextMenu { get; } = new();

    public HierarchyNode<GameEntity> Node { get; }
    
    private readonly IEntityManagementService _entityManagementService;
    private readonly ISelectionService _selectionService;

#pragma warning disable CS8618
    public HierarchyNodeViewModel() { }
#pragma warning restore CS8618

    public HierarchyNodeViewModel(HierarchyNode<GameEntity> node, IEntityManagementService entityManagementService, ISelectionService selectionService)
    {
        Node = node;
        Node.Content.NameChangedEvent += HandleNameChangedEvent;
        _entityManagementService = entityManagementService;
        _selectionService = selectionService;

        Name.Value = node.Content.Name;

        SelectCommand = ReactiveCommand.Create(Select);
        DeleteCommand = ReactiveCommand.Create(Delete);

        StartRenameCommand = new RelayCommand(StartRename);
        ConfirmRenameCommand = ReactiveCommand.Create<string>(ConfirmRename);
        MoveNodeCommand = new MoveNodeCommand(Node, _entityManagementService);

        ContextMenu.AddOption(new ContextMenuViewModel.ContextMenuOption("Rename", () => StartRenameCommand.Execute(null)));
        ContextMenu.AddOption(new ContextMenuViewModel.ContextMenuOption("Delete", Delete));
    }

    public override void Dispose()
    {
        Node.Content.NameChangedEvent -= HandleNameChangedEvent;
    }

    public IEnumerable<HierarchyNodeViewModel> CreateChildNodes(IFactory<HierarchyNodeViewModel> nodeFactory)
    {
        foreach (var childNode in Node.ChildNodes)
        {
            var n = nodeFactory.CreateInstance(childNode);
            ChildNodes.Add(n);
            yield return n;
        }

        foreach (var childNode in ChildNodes)
        {
            foreach (var n in childNode.CreateChildNodes(nodeFactory))
            {
                yield return n;
            }
        }
    }

    public IEnumerable<HierarchyNodeViewModel> GetAllChildNodesRecursive() => ChildNodes.SelectMany(node => node.GetAllChildNodesRecursive());

    public void Select() => Selected.Value = true;
    public void Deselect() => Selected.Value = false;
    
    private void Delete()
    {
        if (Selected.Value)
        {
            Deselect();
            _selectionService.ResetSelection();
        }
        
        _entityManagementService.DestroyEntity(Node.Content);
    }

    private void HandleNameChangedEvent(GameEntity e, string name) => Name.Value = name;
    private void StartRename() => RenameValue.Value = Name.Value;
    private void ConfirmRename(string name) => _entityManagementService.RenameEntity(Node.Content, name);
}