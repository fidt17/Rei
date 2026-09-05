using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Serialization;
using EditorJsonSerializer = ReiEditor.Models.Services.Serialization.JsonSerializer;

namespace ReiEditor.Tests.Models.Services.Serialization;

/// <summary>
/// Verifies JSON callback timing, persisted fields, and deserialization failure boundaries.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Serialization")]
public sealed class JsonSerializerTests
{
    private sealed class TestSerializationCallbacks : IOnSerialization, IOnDeserialized
    {
        public string Name { get; set; } = "before";
        public int Count { get; set; }
        [JsonIgnore] public int SerializationCalls { get; private set; }
        [JsonIgnore] public int DeserializationCalls { get; private set; }
        [JsonIgnore] public string? ObservedName { get; private set; }
        [JsonIgnore] public int ObservedCount { get; private set; }

        public void OnSerialization()
        {
            SerializationCalls++;
            Name = "prepared";
            Count = 7;
        }

        public void OnDeserialized()
        {
            DeserializationCalls++;
            ObservedName = Name;
            ObservedCount = Count;
        }
    }

    private sealed class TestThrowingCallback : IOnSerialization, IOnDeserialized
    {
        public void OnSerialization() => throw new InvalidOperationException("serialization callback failed");
        public void OnDeserialized() => throw new InvalidOperationException("deserialization callback failed");
    }

    /// <summary>
    /// Serialization invokes the callback exactly once before reading values into the JSON object.
    /// </summary>
    [Fact]
    public void SerializePersistsValuesPreparedByCallback()
    {
        var target = new TestSerializationCallbacks();

        var json = new EditorJsonSerializer().Serialize(target);

        Assert.True(JToken.DeepEquals(JObject.Parse("{\"Name\":\"prepared\",\"Count\":7}"), JObject.Parse(json)), json);
        Assert.Equal(1, target.SerializationCalls);
        Assert.Equal(0, target.DeserializationCalls);
    }

    /// <summary>
    /// Deserialization callbacks see all persisted values after the object has been populated.
    /// </summary>
    [Fact]
    public void DeserializeInvokesCallbackAfterPopulatingFixtureValues()
    {
        var value = new EditorJsonSerializer().Deserialize<TestSerializationCallbacks>("{\"Name\":\"loaded\",\"Count\":23}");

        Assert.Equal("loaded", value.Name);
        Assert.Equal(23, value.Count);
        Assert.Equal("loaded", value.ObservedName);
        Assert.Equal(23, value.ObservedCount);
        Assert.Equal(1, value.DeserializationCalls);
        Assert.Equal(0, value.SerializationCalls);
    }

    /// <summary>
    /// A valid document produces its own object even when the fallback overload receives a default object.
    /// </summary>
    [Fact]
    public void DeserializeValidDocumentDoesNotReturnDefaultObject()
    {
        var fallback = new TestSerializationCallbacks { Name = "fallback", Count = 99 };

        var value = new EditorJsonSerializer().Deserialize("{\"Name\":\"loaded\",\"Count\":23}", fallback);

        Assert.NotSame(fallback, value);
        Assert.Equal("loaded", value.Name);
        Assert.Equal(23, value.Count);
        Assert.Equal(1, value.DeserializationCalls);
        Assert.Equal(0, fallback.DeserializationCalls);
    }

    /// <summary>
    /// Invalid JSON is rejected without being converted into a default value.
    /// </summary>
    [Theory]
    [InlineData("{")]
    [InlineData("{\"Count\": nope}")]
    public void DeserializeMalformedJsonThrows(string source)
    {
        Assert.ThrowsAny<JsonException>(() => new EditorJsonSerializer().Deserialize<TestSerializationCallbacks>(source));
    }

    /// <summary>
    /// The required-value overload rejects JSON null with source and destination context.
    /// </summary>
    [Fact]
    public void DeserializeNullThrowsWithTargetContext()
    {
        var error = Assert.Throws<Exception>(() => new EditorJsonSerializer().Deserialize<TestSerializationCallbacks>("null"));

        Assert.Contains("Could not deserialize [null]", error.Message);
        Assert.Contains(nameof(TestSerializationCallbacks), error.Message);
    }

    /// <summary>
    /// Serialization and deserialization callback failures propagate to the caller.
    /// </summary>
    [Fact]
    public void CallbackFailuresPropagate()
    {
        var serializer = new EditorJsonSerializer();

        Assert.Equal("serialization callback failed", Assert.Throws<InvalidOperationException>(() => serializer.Serialize(new TestThrowingCallback())).Message);
        Assert.Equal("deserialization callback failed", Assert.Throws<InvalidOperationException>(() => serializer.Deserialize<TestThrowingCallback>("{}")).Message);
    }
}
