using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

/// <summary>
/// Verifies scalar and enum property editor conversion, writes, and subscription lifetime.
/// </summary>
[Trait("Area", "PropertyEditors")]
public sealed class PrimitivePropertyViewModelTests
{
    /// <summary>
    /// Supplies deterministic enum metadata without production registry initialization.
    /// </summary>
    private sealed class TestSerializableObjectsRegistry : ISerializableObjectsRegistry
    {
        public IEnumerable<SerializableObjectInfo> GetObjects() => [];

        public Task Refresh() => Task.CompletedTask;

        public SerializableObjectInfo? GetObject(string objectName) => null;

        public SerializableEnum? GetEnum(string enumName) => enumName switch
        {
            "Mode" => new SerializableEnum
            {
                EnumName = enumName,
                Options = new Dictionary<string, int> { ["Idle"] = 1, ["Run"] = 2 }
            },
            "AliasedMode" => new SerializableEnum
            {
                EnumName = enumName,
                Options = new Dictionary<string, int> { ["Idle"] = 1, ["Run"] = 2, ["Sprint"] = 2 }
            },
            _ => null
        };
    }

    /// <summary>
    /// Float editor accepts every numeric representation emitted by serialization and maps null to zero.
    /// </summary>
    [Fact]
    public void FloatEditorConvertsSupportedSerializedValues()
    {
        var property = TestProperty(SerializedTypeEnum.Float, 1, "float");
        using var viewModel = new FloatPropertyViewModel(property);

        foreach (var (serialized, expected) in new (object?, float)[]
                 {
                     (2L, 2f), (3.25f, 3.25f), (4.5d, 4.5f)
                 })
        {
            property.Value = serialized;
            Assert.Equal(expected, viewModel.Value);
        }

        var nullProperty = TestProperty(SerializedTypeEnum.Float, null, "float");
        using var nullViewModel = new FloatPropertyViewModel(nullProperty);
        Assert.Equal(0f, nullViewModel.Value);
    }

    /// <summary>
    /// View-model writes update serialized state once, while equal assignments remain silent.
    /// </summary>
    [Fact]
    public void ScalarEditorWritesOnceAndIgnoresEqualValue()
    {
        var property = TestProperty(SerializedTypeEnum.String, "before", "string");
        using var viewModel = new StringPropertyViewModel(property);
        var updates = 0;
        property.ValueChangedEvent += _ => updates++;

        viewModel.Value = "after";
        viewModel.Value = "after";

        Assert.Equal("after", property.Value);
        Assert.Equal(1, updates);
    }

    /// <summary>
    /// Boolean and string editors reject missing or mismatched serialized values.
    /// </summary>
    [Fact]
    public void StrictScalarEditorsRejectUnsupportedValues()
    {
        var boolean = TestProperty(SerializedTypeEnum.Boolean, null, "bool");
        var text = TestProperty(SerializedTypeEnum.String, 12, "string");

        Assert.Throws<Exception>(() => new BooleanPropertyViewModel(boolean));
        Assert.Throws<Exception>(() => new StringPropertyViewModel(text));
    }

    /// <summary>
    /// Disposed scalar editor stops reflecting later serialized changes.
    /// </summary>
    [Fact]
    public void DisposeUnsubscribesScalarEditor()
    {
        var property = TestProperty(SerializedTypeEnum.Boolean, false, "bool");
        var viewModel = new BooleanPropertyViewModel(property);

        viewModel.Dispose();
        property.Value = true;

        Assert.False(viewModel.Value);
    }

    /// <summary>
    /// Enum editor resolves qualified type names and writes selected option values.
    /// </summary>
    [Fact]
    public void EnumEditorMapsQualifiedTypeAndSelectedOption()
    {
        var property = TestProperty(SerializedTypeEnum.Enum, 1, "Game::Mode");
        using var viewModel = new EnumPropertyViewModel(property, new TestSerializableObjectsRegistry());
        var updates = 0;
        property.ValueChangedEvent += _ => updates++;

        Assert.Equal(new[] { "Idle", "Run" }, viewModel.Options);
        Assert.Equal("Idle", viewModel.SelectedValue);

        viewModel.SelectedValue = "Run";
        viewModel.SelectedValue = "Run";

        Assert.Equal(2, property.Value);
        Assert.Equal(2, viewModel.Value);
        Assert.Equal(1, updates);
    }

    /// <summary>
    /// External enum value changes keep selected option synchronized with serialized state.
    /// </summary>
    [Fact]
    public void ExternalEnumChangeUpdatesSelectedOption()
    {
        var property = TestProperty(SerializedTypeEnum.Enum, 1, "Mode");
        using var viewModel = new EnumPropertyViewModel(property, new TestSerializableObjectsRegistry());
        var updates = 0;
        property.ValueChangedEvent += _ => updates++;

        property.Value = 2;

        Assert.Equal("Run", viewModel.SelectedValue);
        Assert.Equal(1, updates);
    }

    /// <summary>
    /// Unknown external enum values preserve selection, notify later listeners, and allow later valid synchronization.
    /// </summary>
    [Fact]
    public void ExternalUnknownEnumValueDoesNotInterruptSynchronization()
    {
        var property = TestProperty(SerializedTypeEnum.Enum, 1, "Mode");
        using var viewModel = new EnumPropertyViewModel(property, new TestSerializableObjectsRegistry());
        var updates = 0;
        property.ValueChangedEvent += _ => updates++;

        property.Value = 99;

        Assert.Equal(99, viewModel.Value);
        Assert.Equal("Idle", viewModel.SelectedValue);
        Assert.Equal(1, updates);

        property.Value = 2;

        Assert.Equal("Run", viewModel.SelectedValue);
        Assert.Equal(2, updates);
    }

    /// <summary>
    /// Selecting an enum alias keeps its label while writing the shared numeric value once.
    /// </summary>
    [Fact]
    public void EnumAliasSelectionKeepsSelectedLabel()
    {
        var property = TestProperty(SerializedTypeEnum.Enum, 1, "AliasedMode");
        using var viewModel = new EnumPropertyViewModel(property, new TestSerializableObjectsRegistry());
        var updates = 0;
        property.ValueChangedEvent += _ => updates++;

        viewModel.SelectedValue = "Sprint";

        Assert.Equal("Sprint", viewModel.SelectedValue);
        Assert.Equal(2, viewModel.Value);
        Assert.Equal(2, property.Value);
        Assert.Equal(1, updates);
    }

    /// <summary>
    /// Disposed enum editor stops mapping later serialized values to selected options.
    /// </summary>
    [Fact]
    public void DisposeUnsubscribesEnumEditor()
    {
        var property = TestProperty(SerializedTypeEnum.Enum, 1, "Mode");
        var viewModel = new EnumPropertyViewModel(property, new TestSerializableObjectsRegistry());

        viewModel.Dispose();
        property.Value = 2;

        Assert.Equal(1, viewModel.Value);
        Assert.Equal("Idle", viewModel.SelectedValue);
    }

    /// <summary>
    /// Missing enum metadata fails construction instead of creating an unusable editor.
    /// </summary>
    [Fact]
    public void EnumEditorRejectsUnknownType()
    {
        var property = TestProperty(SerializedTypeEnum.Enum, 1, "Missing");

        Assert.Throws<Exception>(() => new EnumPropertyViewModel(property, new TestSerializableObjectsRegistry()));
    }

    /// <summary>
    /// Enum editor rejects an initial numeric value absent from known options.
    /// </summary>
    [Fact]
    public void EnumEditorRejectsUnknownInitialValue()
    {
        var property = TestProperty(SerializedTypeEnum.Enum, 99, "Mode");

        Assert.Throws<InvalidOperationException>(() => new EnumPropertyViewModel(property, new TestSerializableObjectsRegistry()));
    }

    private static SerializedProperty TestProperty(SerializedTypeEnum type, object? value, string sourceType)
        => new("_sampleValue", type, value, sourceType, null);

}
