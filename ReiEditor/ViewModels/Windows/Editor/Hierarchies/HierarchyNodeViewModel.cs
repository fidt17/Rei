using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ReactiveUI;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Utils;
using ReiEditor.Utils.Common;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Controls;

namespace ReiEditor.ViewModels.Windows.Editor.Hierarchies;

public class HierarchyNodeViewModel : BaseViewModel
{
    public ICommand SelectCommand { get; }
    public RelayCommand StartRenameCommand { get; } = new();
    public ICommand ConfirmRenameCommand { get; }
    public ICommand DeleteCommand { get; }
	
    public ObservableField<string> Name { get; } = new("Node");
    public ObservableField<string> RenameValue { get; } = new("");
	
    public ObservableField<bool> Selected { get; } = new(false);
    public ObservableField<bool> Expanded { get; } = new(false);

    public ObservableCollection<HierarchyNodeViewModel> Nodes { get; } = new();
    public ContextMenuViewModel ContextMenu { get; } = new();

    private readonly Hierarchy.Node _node;
    private readonly IEntityManagementService _entityManagementService;

#pragma warning disable CS8618
    public HierarchyNodeViewModel() { }
#pragma warning restore CS8618

    public HierarchyNodeViewModel(Hierarchy.Node node, IEntityManagementService entityManagementService)
    {
        _node = node;
        _node.Entity.NameChangedEvent += HandleNameChangedEvent;
        _entityManagementService = entityManagementService;
		
        Name.Value = node.Entity.Name;

        SelectCommand = ReactiveCommand.Create(Select);
        DeleteCommand = ReactiveCommand.Create(Delete);

        StartRenameCommand = new RelayCommand(StartRename);
        ConfirmRenameCommand = ReactiveCommand.Create<string>(ConfirmRename);
        
        ContextMenu.AddOption(new ContextMenuViewModel.ContextMenuOption("Rename", () => StartRenameCommand.Execute(null)));
        ContextMenu.AddOption(new ContextMenuViewModel.ContextMenuOption("Delete", Delete));
    }

    public override void Dispose()
    {
        _node.Entity.NameChangedEvent -= HandleNameChangedEvent;
    }

    public IEnumerable<HierarchyNodeViewModel> GetAllChildNodesRecursive() => Nodes.SelectMany(node => node.GetAllChildNodesRecursive());

    public void Select() => Selected.Value = true;
    public void Deselect() => Selected.Value = false;
    private void Delete() => _entityManagementService.DeleteEntity(_node.Entity);

    private void HandleNameChangedEvent(GameEntity e, string name) => Name.Value = name;
    private void StartRename() => RenameValue.Value = Name.Value;
    private void ConfirmRename(string name) => _entityManagementService.RenameEntity(_node.Entity, name);
}