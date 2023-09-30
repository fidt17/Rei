using Autofac;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Utils.Extensions;
using ReiEditor.Views.Windows.Editor.Build.Commands;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class BuildModule : Module
{
	protected override void Load(ContainerBuilder builder)
	{
		builder.RegisterSingleton<MsBuildService>().As<IBuildService>();
		builder.RegisterSingleton<AssetBuilder>().As<IAssetBuilder>();
		builder.RegisterSingleton<BuildStarter>().As<IBuildStarter>();
		builder.RegisterNonLazy<BuildProcedureTracker>();
		
		builder.RegisterType<BuildProjectCommand>();
	}
}