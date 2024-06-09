using Autofac;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Engine;
using ReiEditor.Models.Services.Windows.Playmode;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor.Playmode;
using ReiEditor.ViewModels.Windows.Editor.Playmode.Commands;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class PlaymodeModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterSingleton<ClientDllManager>().As<IClientDllManager>();
        builder.RegisterSingleton<EngineApi>().As<IEngineApi>();

        builder.RegisterSingleton<PlaymodeStarter>().As<IPlaymodeStarter>();
        builder.RegisterSingleton<PlaymodeService>().As<IPlaymodeService>();
        builder.RegisterSingleton<PlaymodeRunner>().As<IPlaymodeRunner>();
        builder.RegisterSingleton<PlaymodeWindowController>().As<IPlaymodeWindowController>();
		
        builder.RegisterSingleton<EngineLogger>().As<IEngineLogger>();
        builder.RegisterSingleton<EngineShutdownListener>().As<IEngineShutdownListener>();
		
        ConfigureViews(builder);
    }

    private void ConfigureViews(ContainerBuilder builder)
    {
        builder.RegisterType<PlaymodePanelViewModel>();
        builder.RegisterType<StartPlaymodeCommand>();
        builder.RegisterType<StopPlaymodeCommand>();
    }
}