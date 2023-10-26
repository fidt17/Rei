using Autofac;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor.Commands.Entities;
using ReiEditor.ViewModels.Windows.Editor.Hierarchy;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class SceneModule : Module
{
	protected override void Load(ContainerBuilder b)
	{
		b.RegisterSingleton<SceneManagementService>().As<ISceneManagementService>();
		b.RegisterSingleton<EntityManagementService>().As<IEntityManagementService>();
		
		b.RegisterSingleton<HierarchyService>().As<IHierarchyService>();

		b.RegisterType<CreateSceneEntityCommand>();
		
		ConfigureViewModels(b);
	}

	private void ConfigureViewModels(ContainerBuilder b)
	{
		b.RegisterType<HierarchyWindowViewModel>();
		b.RegisterType<HierarchyGameEntityViewModel>();
	}
}