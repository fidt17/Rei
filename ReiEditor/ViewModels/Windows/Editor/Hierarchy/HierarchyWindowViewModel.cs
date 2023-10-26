using System.Collections.ObjectModel;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Controls;
using ReiEditor.ViewModels.Utils;
using ReiEditor.ViewModels.Windows.Editor.Commands.Entities;

namespace ReiEditor.ViewModels.Windows.Editor.Hierarchy;

public class HierarchyWindowViewModel : BaseViewModel
{
	public ObservableField<string> SceneName { get; } = new("Scene Name");
	public ObservableCollection<HierarchyGameEntityViewModel> Nodes { get; } = new();

	public ContextMenuViewModel RootContextMenu { get; } = new();

	private readonly IHierarchyService _hierarchyService;
	private readonly IFactory<HierarchyGameEntityViewModel> _hierarchyElementFactory;
	private readonly CreateSceneEntityCommand _createSceneEntityCommand;

#pragma warning disable CS8618
	public HierarchyWindowViewModel() { }
#pragma warning restore CS8618

	public HierarchyWindowViewModel(
		IHierarchyService hierarchyService, 
		IFactory<HierarchyGameEntityViewModel> hierarchyElementFactory,
		IFactory<CreateSceneEntityCommand> createSceneEntityCommand)
	{
		_hierarchyService = hierarchyService;
		_hierarchyElementFactory = hierarchyElementFactory;
		_createSceneEntityCommand = createSceneEntityCommand.CreateInstance();
		
		_hierarchyService.ActiveHierarchy.Subscribe(HandleActiveHierarchyChangedEvent);

		RootContextMenu.AddOption(new ContextMenuViewModel.ContextMenuOption("New Entity", () => _createSceneEntityCommand.Execute(null)));
	}

	public override void Dispose()
	{
		_createSceneEntityCommand.Dispose();
		
		_hierarchyService.ActiveHierarchy.Unsubscribe(HandleActiveHierarchyChangedEvent);
	}

	private void HandleActiveHierarchyChangedEvent(Models.Services.Hierarchies.Hierarchy? h)
	{
		if (h == null)
		{
			ClearHierarchy();
			return;
		}
		
		SetHierarchy(h);
		UpdateEntitiesList(h);
	}

	private void SetHierarchy(Models.Services.Hierarchies.Hierarchy hierarchy) => SceneName.Set(hierarchy.Name);

	private void ClearHierarchy()
	{
		SceneName.Set("");
		Nodes.ClearAndDispose();
	}

	private void UpdateEntitiesList(Models.Services.Hierarchies.Hierarchy h)
	{
		foreach (var n in h.Nodes)
		{
			Nodes.Add(_hierarchyElementFactory.CreateInstance(n));
		}
	}
}