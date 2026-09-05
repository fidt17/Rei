using System.Numerics;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Utils.SerializedProperties;

namespace ReiEditor.Tests.Utils.Serialization;

/// <summary>
/// Verifies vector extraction from actual property trees and creation of raw vector update dictionaries.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Components")]
public sealed class SerializedPropertyVector3UtilityTests
{
    /// <summary>
    /// All supported numeric representations convert to vector coordinates with independent axis values.
    /// </summary>
    [Theory]
    [InlineData("float")]
    [InlineData("double")]
    [InlineData("int")]
    [InlineData("long")]
    public void TryGetVector3AcceptsSupportedNumericValues(string representation)
    {
        object x = representation switch
        {
            "float" => (object)1.25f,
            "double" => 1.25d,
            "int" => 1,
            "long" => 1L,
            _ => throw new ArgumentOutOfRangeException(nameof(representation))
        };
        var behaviour = CreateBehaviour(new Dictionary<string, SerializedProperty>
        {
            ["x"] = Coordinate("x", x),
            ["y"] = Coordinate("y", -2.5f),
            ["z"] = Coordinate("z", 8d)
        });

        Assert.True(SerializedPropertyVector3Utility.TryGetVector3Property(behaviour, "position", out var vector));
        Assert.Equal(new Vector3(representation is "float" or "double" ? 1.25f : 1f, -2.5f, 8f), vector);
    }

    /// <summary>
    /// Missing roots, wrong containers, missing axes, and invalid axis values fail with a zero output vector.
    /// </summary>
    [Theory]
    [InlineData("missing-root")]
    [InlineData("wrong-container")]
    [InlineData("missing-z")]
    [InlineData("null-y")]
    [InlineData("string-z")]
    public void TryGetVector3RejectsIncompleteOrInvalidShapes(string scenario)
    {
        var children = new Dictionary<string, SerializedProperty>
        {
            ["x"] = Coordinate("x", 1f),
            ["y"] = Coordinate("y", 2f),
            ["z"] = Coordinate("z", 3f)
        };
        if (scenario == "missing-z") children.Remove("z");
        if (scenario == "null-y") children["y"] = Coordinate("y", null);
        if (scenario == "string-z") children["z"] = Coordinate("z", "3");
        var behaviour = scenario == "missing-root" ? new BehaviourComponent(1) : CreateBehaviour(scenario == "wrong-container" ? new List<SerializedProperty>() : children);

        Assert.False(SerializedPropertyVector3Utility.TryGetVector3Property(behaviour, "position", out var vector));
        Assert.Equal(Vector3.Zero, vector);
    }

    /// <summary>
    /// Raw vector dictionaries contain only x, y, and z float values and can update an existing property tree.
    /// </summary>
    [Fact]
    public void CreateVector3ValueProducesNamedFloatUpdates()
    {
        var result = SerializedPropertyVector3Utility.CreateVector3Value(new Vector3(1.25f, -2.5f, 8f));
        var children = new Dictionary<string, SerializedProperty>
        {
            ["x"] = Coordinate("x", 0f),
            ["y"] = Coordinate("y", 0f),
            ["z"] = Coordinate("z", 0f)
        };
        var behaviour = CreateBehaviour(children);

        Assert.Equal(3, result.Count);
        Assert.Equal(1.25f, Assert.IsType<float>(result["x"]));
        Assert.Equal(-2.5f, Assert.IsType<float>(result["y"]));
        Assert.Equal(8f, Assert.IsType<float>(result["z"]));
        behaviour.GetProperty("position").Value = result;

        Assert.Equal(1.25f, Assert.IsType<float>(children["x"].Value));
        Assert.Equal(-2.5f, Assert.IsType<float>(children["y"].Value));
        Assert.Equal(8f, Assert.IsType<float>(children["z"].Value));
    }

    private static BehaviourComponent CreateBehaviour(object value)
    {
        var behaviour = new BehaviourComponent(1);
        behaviour.AddProperty(new SerializedProperty("position", SerializedTypeEnum.Custom, value, "Vector3", null));
        return behaviour;
    }

    private static SerializedProperty Coordinate(string name, object? value)
        => new(name, value is string ? SerializedTypeEnum.String : SerializedTypeEnum.Float, value, "float", null);
}
