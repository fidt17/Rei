using Autofac;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Models.Services.Serialization.Assets;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Startup.Scopes.Editor.Modules;

public class SerializationModule : Module
{
	protected override void Load(ContainerBuilder b)
	{
		b.RegisterSingleton<JsonSerializer>().As<ISerializer>();
		b.RegisterSingleton<BinarySerializer>().As<IBinarySerializer>();

		b.RegisterSingleton<BuildAssetMapSerializer>().As<IBinarySerializer<BuildAssetMap>>();
	}
}