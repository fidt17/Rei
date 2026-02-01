using System.Threading.Tasks;
using Autofac;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.EditorApp.Refresh;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.ProjectManagement.Setup;
using ReiEditor.Models.ProjectManagement.Update;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Resources.EngineResources;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Startup.Common;
using ReiEditor.Startup.EntryPoints;
using ReiEditor.Startup.Scopes.Editor.Modules;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor;
using ReiEditor.ViewModels.Windows.Editor.Project;
using ReiEditor.ViewModels.Windows.Editor.Commands;

namespace ReiEditor.Startup.Scopes.Editor;

public class EditorScope : BaseLifetimeScope
{
    public EditorScope(BaseLifetimeScope parentScope) : base(nameof(EditorScope), parentScope) { }

    protected override async Task OnScopeStart() => await Scope.Resolve<EditorEntryPoint>().Start();

    protected override void ConfigureContainer(ContainerBuilder b)
    {
        b.RegisterSingleton<EditorEntryPoint>();
		
        b.RegisterSingleton<ResourceService>().As<IResourceService>();
        b.RegisterSingleton<EditorProceduresService>().As<IEditorProceduresService>();
        b.RegisterSingleton<ProjectSetupService>().As<IProjectSetupService>();
        b.RegisterSingleton<ProjectUpdateService>().As<IProjectUpdateService>();
        b.RegisterSingleton<EditorRefreshService>().As<IEditorRefreshService>();
		
        b.RegisterType<SaveProjectCommand>();
        b.RegisterSingleton<AssetRegistry>().As<IAssetRegistry>();
        b.RegisterSingleton<AssetCreator>().As<IAssetCreator>();
        b.RegisterSingleton<MetaFilesService>().As<IMetaFilesService>();
        b.RegisterSingleton<AssetImporter>().As<IAssetImporter>();
        b.RegisterSingleton<AssetOperationsService>().As<IAssetOperationsService>();
        b.RegisterSingleton<AssetSearchService>().As<IAssetSearchService>();
        b.RegisterSingleton<EngineResourcesImporter>().As<IEngineResourcesImporter>();
        b.RegisterSingleton<AssetsService>().As<IAssetsService>();
        b.RegisterSingleton<BehaviourRegistry>().As<IBehaviourRegistry>();
        b.RegisterSingleton<BehaviourFileUtility>().As<IBehaviourFileUtility>();
        b.RegisterSingleton<BehaviourComponentsService>().As<IBehaviourComponentsService>();
        b.RegisterSingleton<SerializableObjectsRegistry>().As<ISerializableObjectsRegistry>();
        b.RegisterSingleton<SourceFilesUtility>();

        b.RegisterSingleton<SelectionService>().As<ISelectionService>();

        b.RegisterModule<EngineModule>();
        b.RegisterModule<EditorConsoleModule>();
        b.RegisterModule<SceneModule>();
        b.RegisterModule<MonitorModule>();
        b.RegisterModule<PlaymodeModule>();
        b.RegisterModule<SettingsModule>();
        b.RegisterModule<BuildModule>();
        b.RegisterModule<StatusBarModule>();
		
        ConfigureViewModules(b);
    }

    private void ConfigureViewModules(ContainerBuilder b)
    {
        b.RegisterType<ProjectEditorWindowViewModel>();
        b.RegisterType<ProjectWindowViewModel>();
    }
}
