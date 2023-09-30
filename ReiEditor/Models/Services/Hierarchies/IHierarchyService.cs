using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Hierarchies;

public interface IHierarchyService
{
	IObservable<Hierarchy?> ActiveHierarchy { get; }
}