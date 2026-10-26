using Autofac;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Entities.Sync;
using ReiEditor.Models.Services.RectTransform;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Models.Services.Scenes.Templates;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class SceneModule : Module
{
    protected override void Load(ContainerBuilder b)
    {
        b.RegisterSingleton<SceneManagementService>().As<ISceneManagementService>();
        b.RegisterSingleton<EngineSceneLoader>().As<IEngineSceneLoader>();
        b.RegisterSingleton<SceneAssetDropTargetBuilderService>().As<ISceneAssetDropTargetBuilderService>();
        b.RegisterSingleton<SceneAssetPlacementService>().As<ISceneAssetPlacementService>();
        b.RegisterSingleton<SceneAssetEntityInitializationService>().As<ISceneAssetEntityInitializationService>();
        b.RegisterSingleton<SceneAssetDropService>().As<ISceneAssetDropService>();
        b.RegisterSingleton<SceneAssetDragSessionService>().As<ISceneAssetDragSessionService>();
        b.RegisterSingleton<SceneStateSynchronizer>().As<ISceneStateSynchronizer>();
        b.RegisterSingleton<RectTransformLayoutService>().As<IRectTransformLayoutService>();
        
        b.RegisterSingleton<EntityManagementService>().As<IEntityManagementService>();
        b.RegisterSingleton<EntityDataWriterService>().As<IEntityDataWriterService>();
        b.RegisterSingleton<SelectedEntityActionService>().As<ISelectedEntityActionService>();
        b.RegisterSingleton<SelectedEntityInputService>();
        b.RegisterSingleton<EntityStateApplier>().As<IEntityStateApplier>();
        b.RegisterSingleton<BehaviourSyncService>();
        b.RegisterSingleton<EngineSelectionSyncService>().As<IEngineSelectionSyncService>();
        b.RegisterSingleton<SceneSyncService>().As<ISceneSyncService>().AutoActivate();
        b.RegisterSingleton<EntitySyncService>().As<IEntitySyncService>();

        b.RegisterType<DefaultSceneTemplate>();
		
        ConfigureViewModels(b);
    }

    private void ConfigureViewModels(ContainerBuilder b)
    {
        b.RegisterType<HierarchyWindowViewModel>();
        b.RegisterType<HierarchyNodeViewModel>();
    }
}
