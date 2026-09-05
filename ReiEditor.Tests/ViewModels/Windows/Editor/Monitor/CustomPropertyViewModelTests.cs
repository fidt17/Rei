using Avalonia.Headless.XUnit;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.Vector;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

/// <summary>
/// Verifies vector, color, and nested custom property editor behavior on Avalonia UI thread.
/// </summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "PropertyEditors")]
public sealed class CustomPropertyViewModelTests
{
    /// <summary>
    /// Vector component edit changes only matching serialized child.
    /// </summary>
    [AvaloniaFact]
    public void Vector3EditPreservesOtherComponents()
    {
        var property = TestCustom("position", "Vector3", ("x", 1f), ("y", 2f), ("z", 3f));
        using var viewModel = new Vector3PropertyViewModel(property);

        viewModel.Y = 8.5f;

        var children = TestChildren(property);
        Assert.Equal(1f, children["x"].Value);
        Assert.Equal(8.5f, children["y"].Value);
        Assert.Equal(3f, children["z"].Value);
    }

    /// <summary>
    /// External vector component changes update editor until disposal.
    /// </summary>
    [AvaloniaFact]
    public void Vector2TracksChildUntilDisposed()
    {
        var property = TestCustom("size", "Vector2", ("x", 4f), ("y", 5f));
        var children = TestChildren(property);
        var viewModel = new Vector2PropertyViewModel(property);

        children["x"].Value = 9f;
        Assert.Equal(9f, viewModel.X);

        viewModel.Dispose();
        children["x"].Value = 12f;
        Assert.Equal(9f, viewModel.X);
    }

    /// <summary>
    /// Replacing vector children and disposing editor removes subscriptions from original children.
    /// </summary>
    [AvaloniaFact]
    public void VectorReplacementDisposeUnsubscribesOriginalChildren()
    {
        var property = TestCustom("size", "Vector2", ("x", 1f), ("y", 2f));
        var original = TestChildren(property);
        var viewModel = new Vector2PropertyViewModel(property);
        property.Value = TestChildMap(property, ("x", 7f), ("y", 8f));

        viewModel.Dispose();
        original["x"].Value = 20f;

        Assert.Equal(7f, viewModel.X);
    }

    /// <summary>
    /// Color component edits clamp input and preserve remaining RGBA values.
    /// </summary>
    [AvaloniaFact]
    public void ColorEditClampsAndPreservesOtherComponents()
    {
        var property = TestCustom("tint", "Color", ("r", 0.1f), ("g", 0.2f), ("b", 0.3f), ("a", 0.4f));
        using var viewModel = new ColorPropertyViewModel(property);

        viewModel.R = 2f;

        var children = TestChildren(property);
        Assert.Equal(1f, viewModel.R);
        Assert.Equal(1f, children["r"].Value);
        Assert.Equal(0.2f, children["g"].Value);
        Assert.Equal(0.3f, children["b"].Value);
        Assert.Equal(0.4f, children["a"].Value);
        Assert.Equal("#FF334C66", viewModel.Hex);
    }

    /// <summary>
    /// Valid hexadecimal input updates every serialized color component atomically.
    /// </summary>
    [AvaloniaFact]
    public void ColorHexUpdatesSerializedComponents()
    {
        var property = TestCustom("tint", "Color", ("r", 0f), ("g", 0f), ("b", 0f), ("a", 1f));
        using var viewModel = new ColorPropertyViewModel(property);

        viewModel.Hex = "#336699CC";

        var children = TestChildren(property);
        Assert.Equal(0.2f, Assert.IsType<float>(children["r"].Value), 3);
        Assert.Equal(0.4f, Assert.IsType<float>(children["g"].Value), 3);
        Assert.Equal(0.6f, Assert.IsType<float>(children["b"].Value), 3);
        Assert.Equal(0.8f, Assert.IsType<float>(children["a"].Value), 3);
        Assert.Equal("#336699CC", viewModel.Hex);
    }

    /// <summary>
    /// Replacing generic custom children rebuilds current editors and disposes original editors.
    /// </summary>
    [AvaloniaFact]
    public void CustomEditorReplacementDisposesOriginalEditors()
    {
        var property = TestCustom("settings", "Settings", ("speed", 3f));
        using var viewModel = new CustomPropertyViewModel(property, null!, null!, null!, null!, null!, null!, null!, null!);
        var originalProperty = TestChildren(property)["speed"];
        var originalEditor = Assert.IsType<FloatPropertyViewModel>(Assert.Single(viewModel.Value));

        property.Value = TestChildMap(property, ("speed", 9f));
        originalProperty.Value = 12f;

        Assert.Equal(3f, originalEditor.Value);
        Assert.Equal(9f, Assert.IsType<FloatPropertyViewModel>(Assert.Single(viewModel.Value)).Value);
    }

    /// <summary>
    /// Generic custom editor preserves child editor instances when serialized child references are unchanged.
    /// </summary>
    [AvaloniaFact]
    public void CustomEditorKeepsEditorsForUnchangedChildren()
    {
        var property = TestCustom("settings", "Settings", ("speed", 3f));
        using var viewModel = new CustomPropertyViewModel(property, null!, null!, null!, null!, null!, null!, null!, null!);
        var childEditor = Assert.Single(viewModel.Value);

        property.TriggerChangedEvent();

        Assert.Same(childEditor, Assert.Single(viewModel.Value));
    }

    private static SerializedProperty TestCustom(string name, string sourceType, params (string Name, float Value)[] children)
    {
        var property = new SerializedProperty(name, SerializedTypeEnum.Custom, null, sourceType, null);
        property.Value = TestChildMap(property, children);
        return property;
    }

    private static Dictionary<string, SerializedProperty> TestChildMap(SerializedProperty parent, params (string Name, float Value)[] children)
        => children.ToDictionary(x => x.Name, x => new SerializedProperty(x.Name, SerializedTypeEnum.Float, x.Value, "float", parent));

    private static Dictionary<string, SerializedProperty> TestChildren(SerializedProperty property)
        => Assert.IsType<Dictionary<string, SerializedProperty>>(property.Value);
}
