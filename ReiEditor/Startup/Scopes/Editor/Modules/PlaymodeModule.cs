using Autofac;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Engine;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor.Playmode;
using ReiEditor.ViewModels.Windows.Editor.Playmode.Commands;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class PlaymodeModule : Module
{
	protected override void Load(ContainerBuilder builder)
	{
		builder.RegisterSingleton<ClientDllManager>().As<IClientDllManager>();
		builder.RegisterSingleton<ClientApi>().As<IClientApi>();

		builder.RegisterSingleton<PlaymodeStarter>().As<IPlaymodeStarter>();
		builder.RegisterSingleton<PlaymodeService>().As<IPlaymodeService>();
		builder.RegisterType<PlaymodeRunner>().As<IPlaymodeRunner>().InstancePerDependency();
		
		builder.RegisterType<ClientLogger>().As<IClientLogger>().InstancePerDependency();
		
		ConfigureViews(builder);
	}

	private void ConfigureViews(ContainerBuilder builder)
	{
		builder.RegisterType<PlaymodePanelViewModel>();
		builder.RegisterType<StartPlaymodeCommand>();
		builder.RegisterType<StopPlaymodeCommand>();
	}
}