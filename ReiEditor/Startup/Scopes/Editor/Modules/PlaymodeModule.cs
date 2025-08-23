using Autofac;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Windows.Playmode;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Windows.Editor.Playmode;
using ReiEditor.ViewModels.Windows.Editor.Playmode.Commands;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class PlaymodeModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterSingleton<PlaymodeStarter>().As<IPlaymodeStarter>();
        builder.RegisterSingleton<EngineRunner>().As<IEngineRunner>();
        builder.RegisterSingleton<EngineWindowController>().As<IEngineWindowController>();
		
        ConfigureViews(builder);
    }

    private void ConfigureViews(ContainerBuilder builder)
    {
        builder.RegisterType<PlaymodePanelViewModel>();
        builder.RegisterType<StartPlaymodeCommand>();
        builder.RegisterType<StopPlaymodeCommand>();
    }
}