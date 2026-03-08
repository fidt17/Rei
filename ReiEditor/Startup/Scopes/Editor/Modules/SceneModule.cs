using Autofac;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Entities.Sync;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Models.Services.Scenes.Templates;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor.Commands.Entities;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class SceneModule : Module
{
    protected override void Load(ContainerBuilder b)
    {
        b.RegisterSingleton<SceneManagementService>().As<ISceneManagementService>();
        b.RegisterSingleton<SceneStateSynchronizer>().As<ISceneStateSynchronizer>();
        
        b.RegisterSingleton<EntityManagementService>().As<IEntityManagementService>();
        b.RegisterSingleton<SelectedEntityEditorActionService>().As<ISelectedEntityEditorActionService>();
        b.RegisterSingleton<EntityStateSynchronizer>().As<IEntityStateSynchronizer>();

        b.RegisterType<CreateSceneEntityCommand>();

        b.RegisterType<DefaultSceneTemplate>();
		
        ConfigureViewModels(b);
    }

    private void ConfigureViewModels(ContainerBuilder b)
    {
        b.RegisterType<HierarchyWindowViewModel>();
        b.RegisterType<HierarchyNodeViewModel>();
    }
}
