using System.Collections.ObjectModel;

namespace ReiEditor.ViewModels.Windows.Editor.Hierarchy;

public class HierarchyGameEntityViewModel
{
	public string Name { get; } = "Entity Name";
	public ObservableCollection<HierarchyGameEntityViewModel> Nodes { get; } = new();

#pragma warning disable CS8618
	public HierarchyGameEntityViewModel() { }
#pragma warning restore CS8618

	public HierarchyGameEntityViewModel(Models.Services.Hierarchies.Hierarchy.Node node)
	{
		Name = node.Entity.Name;
	}
}