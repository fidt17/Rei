using Autofac;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Capture;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Engine.Input;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Engine;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class EngineModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterSingleton<ClientDllManager>().As<IClientDllManager>();
        builder.RegisterSingleton<EngineApi>().As<IEngineApi>();
        builder.RegisterSingleton<EngineFrameCaptureService>().As<IEngineFrameCaptureService>();
        builder.RegisterSingleton<EntityApi>().As<IEntityApi>();
        builder.RegisterSingleton<AssetApi>().As<IAssetApi>();
		
        builder.RegisterSingleton<EngineLogger>().As<IEngineLogger>();
        builder.RegisterSingleton<EngineInputService>().As<IEngineInputService>();
        builder.RegisterSingleton<EngineShutdownListener>().As<IEngineShutdownListener>();
    }
}
