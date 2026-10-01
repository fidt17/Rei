using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

public sealed class PropertyDisplayUtilsTests
{
    [Fact]
    public void DisplayUsesSourceOrderAndSuppressesHiddenAnchorHeaderWithoutChangingValues()
    {
        var values = new[] { Value("Alpha", 11), Value("Hidden", 22), Value("Zulu", 33), Value("Unknown", 44) };
        var schema = new Dictionary<string, SerializableObjectInfo.SerializedPropertyData>
        {
            ["Alpha"] = Metadata(1),
            ["Hidden"] = Metadata(2, "Diagnostics", true),
            ["Zulu"] = Metadata(0, "Palette")
        };
        var before = SerializedPropertyJsonConverter.SerializeRuntimeProperties(values);
        var displayed = PropertyDisplayUtils.GetVisibleProperties(values, schema).ToList();
        var rows = PropertyDisplayUtils.CreateRows(displayed, schema, property => new PropertyNameViewModel(property)).ToList();
        Assert.Equal(new[] { "Zulu", "Alpha", "Unknown" }, displayed.Select(property => property.Name));
        Assert.Collection(rows,
            row => Assert.Equal("Palette", Assert.IsType<PropertyHeaderViewModel>(row).Text),
            row => Assert.Equal("Zulu", Assert.IsType<PropertyNameViewModel>(row).Value),
            row => Assert.Equal("Alpha", Assert.IsType<PropertyNameViewModel>(row).Value),
            row => Assert.Equal("Unknown", Assert.IsType<PropertyNameViewModel>(row).Value));
        Assert.True(JToken.DeepEquals(before, SerializedPropertyJsonConverter.SerializeRuntimeProperties(values)));
        Assert.Same(values[2], displayed[0]);
        Assert.DoesNotContain("HeaderBefore", before.ToString());
        foreach (var row in rows) row.Dispose();
    }

    [Fact]
    public void MissingFieldsDoNotCreateHeadersAndSchemaLessValuesKeepTheirOrder()
    {
        var values = new[] { Value("Second", 2), Value("First", 1) };
        var schema = new Dictionary<string, SerializableObjectInfo.SerializedPropertyData> { ["Missing"] = Metadata(0, "Empty") };
        Assert.Equal(values, PropertyDisplayUtils.GetVisibleProperties(values, null));
        var rows = PropertyDisplayUtils.CreateRows(PropertyDisplayUtils.GetVisibleProperties(values, schema), schema, property => new PropertyNameViewModel(property)).ToList();
        Assert.All(rows, row => Assert.IsType<PropertyNameViewModel>(row));
        foreach (var row in rows) row.Dispose();
    }

    private static SerializedProperty Value(string name, int value) => new(name, SerializedTypeEnum.Integer, value, "i32", null);
    private static SerializableObjectInfo.SerializedPropertyData Metadata(int index, string? header = null, bool hidden = false) => new(SerializedTypeEnum.Integer, "i32", null, SerializedTypeEnum.Invalid, null, null, null, hidden, header, index);
}
