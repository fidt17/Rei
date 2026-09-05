using Autofac;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Startup.Scopes.Editor.Modules;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Factory;

namespace ReiEditor.Tests.Startup;

/// <summary>
/// Verifies production serialization registrations and factory resolution in a minimal Autofac container.
/// </summary>
[Trait("Category", "Composition")]
[Trait("Area", "Startup")]
public sealed class SerializationModuleTests
{
    /// <summary>
    /// The registered binary serializer writes the expected asset map bytes and serializers retain singleton lifetimes.
    /// </summary>
    [Fact]
    public void ModuleResolvesBinarySerializerAndWritesAssetMapWireFormat()
    {
        using var container = CreateContainer();
        var serializer = container.Resolve<IBinarySerializer>();
        var map = new BuildAssetMap();
        map.Add(new BuildAssetMap.AssetBuildInfo("A", "B", "source.png", "C", 42));
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        serializer.Serialize(map, writer);
        writer.Flush();

        Assert.Equal(Convert.FromHexString("010000000100000041010000004201000000432A00000000000000"), stream.ToArray());
        Assert.Same(serializer, container.Resolve<IBinarySerializer>());
        Assert.Same(container.Resolve<ISerializer>(), container.Resolve<ISerializer>());
    }

    /// <summary>
    /// A factory passes typed constructor parameters and creates separate scene instances for separate calls.
    /// </summary>
    [Fact]
    public void FactoryResolvesSceneWithTypedConstructorParameter()
    {
        using var container = CreateContainer();
        var factory = container.Resolve<IFactory<Scene>>();

        var first = factory.CreateInstance("First scene");
        var second = factory.CreateInstance("Second scene");

        Assert.Equal("First scene", first.Name);
        Assert.Equal("Second scene", second.Name);
        Assert.NotSame(first, second);
        Assert.Empty(first.Entities);
    }

    private static IContainer CreateContainer()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<SerializationModule>();
        builder.RegisterGeneric(typeof(TestLogger<>)).As(typeof(ILogger<>));
        builder.RegisterGeneric(typeof(Factory<>)).As(typeof(IFactory<>));
        builder.RegisterType<Scene>();
        return builder.Build();
    }
}
