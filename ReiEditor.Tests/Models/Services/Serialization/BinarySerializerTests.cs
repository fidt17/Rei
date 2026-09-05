using System.Text;
using Autofac;
using Autofac.Core.Registration;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Models.Services.Serialization.Assets;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Tests.Models.Services.Serialization;

/// <summary>
/// Verifies typed binary dispatch and asset-map bytes independently of the production writer.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Serialization")]
public sealed class BinarySerializerTests
{
    private sealed class TestBinaryTarget
    {
    }

    private sealed class TestBinarySerializer : IBinarySerializer<TestBinaryTarget>
    {
        public int Calls { get; private set; }
        public TestBinaryTarget? Target { get; private set; }
        public BinaryWriter? Writer { get; private set; }
        public Exception? Failure { get; init; }

        public void Serialize(TestBinaryTarget target, BinaryWriter writer)
        {
            Calls++;
            Target = target;
            Writer = writer;
            if (Failure != null) throw Failure;
            writer.Write((byte)0xA5);
        }
    }

    /// <summary>
    /// String bytes contain a little-endian UTF-16 character count followed by the writer's UTF-8 payload.
    /// </summary>
    [Theory]
    [InlineData("", "00000000")]
    [InlineData("A", "0100000041")]
    [InlineData("Ж", "01000000D096")]
    [InlineData("😀", "02000000F09F9880")]
    public void WriteStringMatchesIndependentUtf8Bytes(string value, string expectedHex)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);

        writer.WriteString(value);
        writer.Flush();

        Assert.Equal(Convert.FromHexString(expectedHex), stream.ToArray());
    }

    /// <summary>
    /// Empty maps contain only the four-byte zero entry count.
    /// </summary>
    [Fact]
    public void EmptyAssetMapWritesZeroCount()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        new BuildAssetMapSerializer().Serialize(new BuildAssetMap(), writer);
        writer.Flush();

        Assert.Equal(new byte[] { 0, 0, 0, 0 }, stream.ToArray());
    }

    /// <summary>
    /// Two map entries preserve insertion order and write Id, Name, Path, then the full 64-bit offset.
    /// </summary>
    [Fact]
    public void AssetMapMatchesIndependentTwoEntryWireFixture()
    {
        var map = new BuildAssetMap();
        map.Add(new BuildAssetMap.AssetBuildInfo("Z", "First", "excluded-source", "a/b", 0x0102030405060708));
        map.Add(new BuildAssetMap.AssetBuildInfo("A", "", "also-excluded", "c", 9));
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        new BuildAssetMapSerializer().Serialize(map, writer);
        writer.Flush();

        const string EXPECTED_HEX = "02000000" +
            "010000005A05000000466972737403000000612F620807060504030201" +
            "01000000410000000001000000630900000000000000";
        Assert.Equal(Convert.FromHexString(EXPECTED_HEX), stream.ToArray());
    }

    /// <summary>
    /// Generic dispatch forwards the same target and writer exactly once and leaves caller-owned streams usable.
    /// </summary>
    [Fact]
    public void DispatchForwardsTargetAndWriterWithoutDisposingThem()
    {
        var probe = new TestBinarySerializer();
        var builder = new ContainerBuilder();
        builder.RegisterInstance(probe).As<IBinarySerializer<TestBinaryTarget>>();
        using var container = builder.Build();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var target = new TestBinaryTarget();

        new BinarySerializer(container).Serialize(target, writer);
        writer.Write((byte)0x5A);
        writer.Flush();

        Assert.Equal(1, probe.Calls);
        Assert.Same(target, probe.Target);
        Assert.Same(writer, probe.Writer);
        Assert.Equal(new byte[] { 0xA5, 0x5A }, stream.ToArray());
    }

    /// <summary>
    /// Missing typed serializers fail resolution before any bytes are written.
    /// </summary>
    [Fact]
    public void MissingTypedSerializerThrowsWithoutWriting()
    {
        using var container = new ContainerBuilder().Build();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        Assert.Throws<ComponentNotRegisteredException>(() => new BinarySerializer(container).Serialize(new TestBinaryTarget(), writer));
        Assert.Empty(stream.ToArray());
    }

    /// <summary>
    /// A resolved serializer's exception propagates unchanged rather than becoming a resolution or success result.
    /// </summary>
    [Fact]
    public void TypedSerializerFailurePropagatesUnchanged()
    {
        var failure = new InvalidDataException("test payload rejected");
        var probe = new TestBinarySerializer { Failure = failure };
        var builder = new ContainerBuilder();
        builder.RegisterInstance(probe).As<IBinarySerializer<TestBinaryTarget>>();
        using var container = builder.Build();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        Assert.Same(failure, Assert.Throws<InvalidDataException>(() => new BinarySerializer(container).Serialize(new TestBinaryTarget(), writer)));
        Assert.Equal(1, probe.Calls);
        Assert.Empty(stream.ToArray());
    }
}
