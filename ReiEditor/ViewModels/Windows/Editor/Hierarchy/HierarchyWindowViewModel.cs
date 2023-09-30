using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Utils.Common;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Hierarchy;

public class HierarchyWindowViewModel : BaseViewModel
{
	public ObservableField<string> SceneName { get; } = new("Scene Name");

	private readonly IHierarchyService _hierarchyService;

#pragma warning disable CS8618
	public HierarchyWindowViewModel() { }
#pragma warning restore CS8618

	public HierarchyWindowViewModel(IHierarchyService hierarchyService)
	{
		_hierarchyService = hierarchyService;
		
		_hierarchyService.ActiveHierarchy.Subscribe(HandleActiveHierarchyChangedEvent);
	}

	public override void Dispose()
	{
		_hierarchyService.ActiveHierarchy.Unsubscribe(HandleActiveHierarchyChangedEvent);
	}

	private void HandleActiveHierarchyChangedEvent(Models.Services.Hierarchies.Hierarchy? hierarchy)
	{
		if (hierarchy == null)
		{
			ClearHierarchy();
			return;
		}
		
		SetHierarchy(hierarchy);
	}

	private void SetHierarchy(Models.Services.Hierarchies.Hierarchy hierarchy)
	{
		SceneName.Set(hierarchy.Name);
	}

	private void ClearHierarchy()
	{
		SceneName.Set("");
	}
}