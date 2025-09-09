using Autofac;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Build.Solution;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor.Commands;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class BuildModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterSingleton<SourceTracker>().As<ISourceTracker>();
        builder.RegisterSingleton<MsBuildSolutionBuilder>().As<ISolutionBuilder>();
        builder.RegisterSingleton<BuildService>().As<IBuildService>();
        builder.RegisterSingleton<AssetBuilder>().As<IAssetBuilder>();
        builder.RegisterSingleton<BuildStarter>().As<IBuildStarter>();
        builder.RegisterNonLazy<BuildProcedureTracker>();
		
        builder.RegisterType<BuildProjectCommand>();
        builder.RegisterType<ImportEngineResourcesCommand>();
    }
}