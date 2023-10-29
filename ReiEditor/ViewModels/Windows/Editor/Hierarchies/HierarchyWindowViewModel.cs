using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ReactiveUI;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Controls;
using ReiEditor.ViewModels.Utils;
using ReiEditor.ViewModels.Windows.Editor.Commands.Entities;

namespace ReiEditor.ViewModels.Windows.Editor.Hierarchies;

public class HierarchyWindowViewModel : BaseViewModel
{
	public ICommand ResetSelectionCommand { get; }
	
	public ObservableField<string> SceneName { get; } = new("Scene Name");
	public ObservableCollection<HierarchyNodeViewModel> Nodes { get; } = new();

	public ContextMenuViewModel RootContextMenu { get; } = new();

	private Hierarchy? _activeHierarchy;
	
	private readonly IHierarchyService _hierarchyService;
	private readonly IFactory<HierarchyNodeViewModel> _hierarchyElementFactory;
	private readonly CreateSceneEntityCommand _createSceneEntityCommand;

#pragma warning disable CS8618
	public HierarchyWindowViewModel() { }
#pragma warning restore CS8618

	public HierarchyWindowViewModel(
		IHierarchyService hierarchyService, 
		IFactory<HierarchyNodeViewModel> hierarchyElementFactory,
		IFactory<CreateSceneEntityCommand> createSceneEntityCommand)
	{
		_hierarchyService = hierarchyService;
		_hierarchyElementFactory = hierarchyElementFactory;
		_createSceneEntityCommand = createSceneEntityCommand.CreateInstance();
		
		_hierarchyService.ActiveHierarchy.Subscribe(HandleActiveHierarchyChangedEvent);

		ResetSelectionCommand = ReactiveCommand.Create(ResetSelection);
		
		RootContextMenu.AddOption(new ContextMenuViewModel.ContextMenuOption("New Entity", () => _createSceneEntityCommand.Execute(null)));
	}

	public override void Dispose()
	{
		_createSceneEntityCommand.Dispose();
		
		_hierarchyService.ActiveHierarchy.Unsubscribe(HandleActiveHierarchyChangedEvent);

		ResetCurrentHierarchy();
	}

	private void HandleActiveHierarchyChangedEvent(Hierarchy? h)
	{
		if (h == null)
		{
			ResetCurrentHierarchy();
			return;
		}
		
		SetHierarchy(h);
	}

	private void ResetCurrentHierarchy()
	{
		if (_activeHierarchy == null) return;
		
		ClearHierarchy();
		_activeHierarchy.ChangedEvent -= UpdateEntitiesList;

		_activeHierarchy = null;
	}

	private void SetHierarchy(Hierarchy hierarchy)
	{
		if (_activeHierarchy != null) ResetCurrentHierarchy();
		
		_activeHierarchy = hierarchy;
		_activeHierarchy.ChangedEvent += UpdateEntitiesList;
		
		SceneName.Set(hierarchy.Name);
		UpdateEntitiesList(_activeHierarchy);
	}

	private void ClearHierarchy()
	{
		SceneName.Set("");
		Nodes.ClearAndDispose();
	}

	private void UpdateEntitiesList(Hierarchy h)
	{
		Nodes.ClearAndDispose();
		
		foreach (var n in h.Nodes)
		{
			var node = _hierarchyElementFactory.CreateInstance(n);
			node.Selected.ChangedEvent += (b) => HandleNodeSelectedChangedEvent(node, b);
			Nodes.Add(node);
		}
	}

	private void ResetSelection()
	{
		foreach (var n in GetAllNodes().Where(n => n.Selected.Value))
		{
			n.Deselect();
		}
	}
	
	private void HandleNodeSelectedChangedEvent(HierarchyNodeViewModel node, bool isSelected)
	{
		if (!isSelected) return;
		
		foreach (var n in GetAllNodes())
		{
			if (n == node) continue;
			n.Deselect();
		}
	}

	private IEnumerable<HierarchyNodeViewModel> GetAllNodes()
	{
		foreach (var node in Nodes)
		{
			yield return node;
			foreach (var childNodes in node.GetAllChildNodesRecursive())
			{
				yield return childNodes;
			}
		}
	}
}