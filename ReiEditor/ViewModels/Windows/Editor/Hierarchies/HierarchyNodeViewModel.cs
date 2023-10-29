using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ReactiveUI;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Utils.Common;
using ReiEditor.ViewModels.Controls;

namespace ReiEditor.ViewModels.Windows.Editor.Hierarchies;

public class HierarchyNodeViewModel
{
	public ICommand SelectCommand { get; }
	public ICommand DeleteCommand { get; }
	
	public string Name { get; } = "Entity Name";
	
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
		_entityManagementService = entityManagementService;
		
		Name = node.Entity.Name;

		SelectCommand = ReactiveCommand.Create(Select);
		DeleteCommand = ReactiveCommand.Create(Delete);
		
		ContextMenu.AddOption(new ContextMenuViewModel.ContextMenuOption("Delete", Delete));
	}


	public IEnumerable<HierarchyNodeViewModel> GetAllChildNodesRecursive()
	{
		foreach (var node in Nodes)
		{
			foreach (var childNode in node.GetAllChildNodesRecursive())
			{
				yield return childNode;
			}
		}
	}

	public void Select() => Selected.Value = true;
	public void Deselect() => Selected.Value = false;
	private void Delete() => _entityManagementService.DeleteEntity(_node.Entity);
}