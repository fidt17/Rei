using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Input;
using ReactiveUI;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Utils;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Controls;

namespace ReiEditor.ViewModels.Windows.Editor.Hierarchies;

public class HierarchyNodeViewModel : BaseViewModel
{
    public ICommand SelectCommand { get; }
    public RelayCommand StartRenameCommand { get; } = new();
    public ICommand ConfirmRenameCommand { get; }
    public ICommand DeleteCommand { get; }
    public MoveNodeCommand MoveNodeCommand { get; }
	
    public ObservableField<string> Name { get; } = new("Node");
    public ObservableField<string> RenameValue { get; } = new("");
	
    public ObservableField<bool> Selected { get; } = new(false);
    public ObservableField<bool> Expanded { get; } = new(false);

    public ObservableCollection<HierarchyNodeViewModel> ChildNodes { get; } = new();
    public ContextMenuViewModel ContextMenu { get; } = new();

    public Hierarchy.Node Node { get; }
    
    private readonly IEntityManagementService _entityManagementService;

#pragma warning disable CS8618
    public HierarchyNodeViewModel() { }
#pragma warning restore CS8618

    public HierarchyNodeViewModel(Hierarchy.Node node, IEntityManagementService entityManagementService)
    {
        Node = node;
        Node.Entity.NameChangedEvent += HandleNameChangedEvent;
        _entityManagementService = entityManagementService;
		
        Name.Value = node.Entity.Name + " " + node.Entity.Transform._order;

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
        Node.Entity.NameChangedEvent -= HandleNameChangedEvent;
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
    private void Delete() => _entityManagementService.DeleteEntity(Node.Entity);

    private void HandleNameChangedEvent(GameEntity e, string name) => Name.Value = name;
    private void StartRename() => RenameValue.Value = Name.Value;
    private void ConfirmRename(string name) => _entityManagementService.RenameEntity(Node.Entity, name);
}