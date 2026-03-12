using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Input;
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
    public RelayCommand StartRenameCommand { get; } = new();
    public ICommand ConfirmRenameCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand DeleteCommand { get; }

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
    private Action<HierarchyNodeViewModel, KeyModifiers>? _selectionRequestedAction;
    private Action<HierarchyNodeViewModel>? _contextMenuSelectionRequestedAction;

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

        DuplicateCommand = ReactiveCommand.Create(Duplicate);
        DeleteCommand = ReactiveCommand.Create(Delete);

        StartRenameCommand = new RelayCommand(StartRename);
        ConfirmRenameCommand = ReactiveCommand.Create<string>(ConfirmRename);
        ContextMenu.AddOption(new ContextMenuOption("Rename", () => StartRenameCommand.Execute(null)));
        ContextMenu.AddOption(new ContextMenuOption("Duplicate", () => DuplicateCommand.Execute(null)));
        ContextMenu.AddOption(new ContextMenuOption("Delete", Delete));
        
        _selectionService.RegisterSelectable(this);
    }

    public override void Dispose()
    {
        _selectionService.UnregisterSelectable(this);
        
        Node.Content.NameChangedEvent -= HandleNameChangedEvent;
    }

    public IEnumerable<HierarchyNodeViewModel> CreateChildNodes(IFactory<HierarchyNodeViewModel> nodeFactory)
    {
        foreach (var childNode in Node.ChildNodes.ToArray())
        {
            var n = nodeFactory.CreateInstance(childNode);
            ChildNodes.Add(n);
            yield return n;
        }

        foreach (var childNode in ChildNodes.ToArray())
        {
            foreach (var n in childNode.CreateChildNodes(nodeFactory))
            {
                yield return n;
            }
        }
    }

    public IEnumerable<HierarchyNodeViewModel> GetAllChildNodesRecursive() => ChildNodes.ToArray().SelectMany(node => node.GetAllChildNodesRecursive());

    public void ConfigureSelectionActions(
        Action<HierarchyNodeViewModel, KeyModifiers> selectionRequestedAction,
        Action<HierarchyNodeViewModel> contextMenuSelectionRequestedAction)
    {
        _selectionRequestedAction = selectionRequestedAction;
        _contextMenuSelectionRequestedAction = contextMenuSelectionRequestedAction;
    }

    public void RequestSelection(KeyModifiers modifiers) => _selectionRequestedAction?.Invoke(this, modifiers);
    public void RequestContextMenuSelection() => _contextMenuSelectionRequestedAction?.Invoke(this);
    public void Select() => Selected.Value = true;
    public void Deselect() => Selected.Value = false;
    public void SetSelected(bool selected) => Selected.Value = selected;
    
    public IReadOnlyList<int> GetDraggedEntityIds()
    {
        if (!Selected.Value) return new[] { Node.Content.Id };

        return _selectionService.SelectedItems
            .OfType<IEntitySelectable>()
            .Select(x => x.Entity.Id)
            .Distinct()
            .ToArray();
    }

    private void Delete()
    {
        if (Selected.Value)
        {
            _selectionService.ResetSelection();
        }
        
        _entityManagementService.DestroyEntity(Node.Content);
    }

    private void Duplicate()
    {
        _entityManagementService.InstantiateEntity(Node.Content);
    }

    private void HandleNameChangedEvent(GameEntity e, string name) => Name.Value = name;
    private void StartRename() => RenameValue.Value = Name.Value;
    private void ConfirmRename(string name) => _entityManagementService.RenameEntity(Node.Content, name);
}
