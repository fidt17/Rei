using Autofac;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor.Hierarchy;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class HierarchyModule : Module
{
	protected override void Load(ContainerBuilder builder)
	{
		builder.RegisterSingleton<HierarchyService>().As<IHierarchyService>();
		
		builder.RegisterType<HierarchyWindowViewModel>();
	}
}