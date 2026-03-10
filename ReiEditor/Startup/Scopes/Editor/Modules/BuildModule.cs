using Autofac;
using ReiEditor.Models.EditorApp.ProjectBuildWindow;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.ProjectBuild;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Build.Assets.Cache;
using ReiEditor.Models.Services.Build.Solution;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor.Commands;
using ReiEditor.ViewModels.Windows.Editor.BuildProject;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class BuildModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterSingleton<BuildPreparationService>().As<IBuildPreparationService>();
        builder.RegisterSingleton<EngineBuildGate>().As<IEngineBuildGate>();
        builder.RegisterSingleton<EditorBuildOutputService>().As<IEditorBuildOutputService>();
        builder.RegisterSingleton<ProjectBuildStateService>().As<IProjectBuildStateService>();
        builder.RegisterSingleton<StagedEditorBuildService>().As<IStagedEditorBuildService>();
        builder.RegisterSingleton<MsBuildSolutionBuilder>().As<ISolutionBuilder>();
        builder.RegisterSingleton<BuildService>().As<IBuildService>();
        builder.RegisterSingleton<AssetBuildCacheService>().As<IAssetBuildCacheService>();
        builder.RegisterSingleton<AssetBuildCachePipeline>().As<IAssetBuildCachePipeline>();
        builder.RegisterSingleton<AssetBuildEngineSessionFactory>().As<IAssetBuildEngineSessionFactory>();
        builder.RegisterSingleton<AssetBuilder>().As<IAssetBuilder>();
        builder.RegisterSingleton<BuildStarter>().As<IBuildStarter>();
        builder.RegisterSingleton<ProjectBuildWindowService>().As<IProjectBuildWindowService>();
        builder.RegisterSingleton<ProjectBuildService>().As<IProjectBuildService>();
        builder.RegisterType<ProjectBuildConfigurationUtility>();
        builder.RegisterType<ProjectBuildOutputPathUtility>();
        builder.RegisterNonLazy<BuildProcedureTracker>();
        
        builder.RegisterType<BuildProjectCommand>();
        builder.RegisterType<OpenBuildProjectWindowCommand>();
        builder.RegisterType<ImportEngineResourcesCommand>();
        builder.RegisterType<BuildProjectWindowViewModel>();
    }
}
