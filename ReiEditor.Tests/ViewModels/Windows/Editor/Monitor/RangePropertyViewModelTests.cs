using System.Globalization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

[Trait("Area", "PropertyEditors")]
public sealed class RangePropertyViewModelTests
{
    [Fact]
    public void OldOutOfRangeAndExternalValuesAreDisplayedWithoutWrites()
    {
        var property = Integer(12L);
        var writes = 0;
        property.ValueChangedEvent += _ => writes++;
        using var vm = new RangePropertyViewModel(property, new(0, 8, null, true));
        Assert.Equal("12", vm.InputText);
        Assert.Equal(8, vm.SliderValue);
        Assert.True(vm.HasStatus);
        vm.ApplyInput();
        vm.CancelInput();
        Assert.Equal(12L, property.Value);
        Assert.Equal(0, writes);
        property.Value = -4;
        Assert.Equal("-4", vm.InputText);
        Assert.Equal(0, vm.SliderValue);
        Assert.Equal(1, writes);
        vm.SetSliderValue(3.5);
        Assert.Equal(4, property.Value);
        Assert.Equal(2, writes);
        Assert.False(vm.HasStatus);
    }

    [Theory]
    [InlineData(-100, 0)]
    [InlineData(1.49, 1)]
    [InlineData(1.5, 2)]
    [InlineData(100, 8)]
    public void IntegerSliderRoundsAndClamps(double input, int expected)
    {
        var property = Integer(4);
        using var vm = new RangePropertyViewModel(property, new(0, 8, null, true));
        vm.SetSliderValue(input);
        Assert.Equal(expected, Assert.IsType<int>(property.Value));
    }

    [Fact]
    public void EqualNumericValuesAndDisposedEditorsDoNotWrite()
    {
        var property = Integer(4L);
        var writes = 0;
        property.ValueChangedEvent += _ => writes++;
        var vm = new RangePropertyViewModel(property, new(0, 8, null, true));
        vm.SetSliderValue(4);
        Assert.Equal(0, writes);
        vm.Dispose();
        vm.SetSliderValue(6);
        vm.InputText = "2";
        vm.ApplyInput();
        property.Value = 1;
        Assert.Equal(4, vm.SliderValue);
        Assert.Equal(1, writes);
    }

    [Theory]
    [InlineData("-5", 0)]
    [InlineData("30", 8)]
    [InlineData("3", 3)]
    public void KeyboardIntegersClampOnCommit(string text, int expected)
    {
        var property = Integer(4);
        using var vm = new RangePropertyViewModel(property, new(0, 8, null, true));
        vm.InputText = text;
        Assert.Equal(4, property.Value);
        vm.ApplyInput();
        Assert.Equal(expected, property.Value);
    }

    [Theory]
    [InlineData("2.5")]
    [InlineData("2147483648")]
    [InlineData("invalid")]
    [InlineData("NaN")]
    public void InvalidIntegerInputLeavesValueUnchangedAndCanBeCancelled(string text)
    {
        var property = Integer(4);
        using var vm = new RangePropertyViewModel(property, new(0, 8, null, true));
        vm.InputText = text;
        vm.ApplyInput();
        Assert.Equal(4, property.Value);
        Assert.True(vm.HasStatus);
        vm.CancelInput();
        Assert.Equal("4", vm.InputText);
        Assert.False(vm.HasStatus);
    }

    [Fact]
    public void FloatStepAppliesToSliderButKeepsPreciseKeyboardInput()
    {
        var property = Float(0.5);
        using var vm = new RangePropertyViewModel(property, new(0, 1, 0.25, false));
        vm.SetSliderValue(0.38);
        Assert.Equal(0.5f, Convert.ToSingle(property.Value));
        vm.SetSliderValue(0.26);
        Assert.Equal(0.25f, Assert.IsType<float>(property.Value));
        vm.InputText = "0.1234567";
        vm.ApplyInput();
        Assert.Equal(0.1234567f, Assert.IsType<float>(property.Value));
    }

    [Fact]
    public void SliderUpperEndpointRemainsReachableOffStepGrid()
    {
        var property = Integer(0);
        using var vm = new RangePropertyViewModel(property, new(0, 5, 2, true));
        vm.SetSliderValue(4.9);
        Assert.Equal(5, property.Value);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e100")]
    [InlineData("no")]
    public void InvalidFloatInputNeverWrites(string text)
    {
        var property = Float(0.5f);
        using var vm = new RangePropertyViewModel(property, new(0, 1, null, false));
        vm.InputText = text;
        vm.ApplyInput();
        vm.SetSliderValue(double.NaN);
        vm.SetSliderValue(double.PositiveInfinity);
        Assert.Equal(0.5f, property.Value);
        Assert.True(vm.HasStatus);
    }

    [Fact]
    public void FloatInputAcceptsLocalAndInvariantDecimalSeparators()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            var property = Float(0f);
            using var vm = new RangePropertyViewModel(property, new(0, 1, null, false));
            foreach (var text in new[] { "0,25", "0.5" })
            {
                vm.InputText = text;
                vm.ApplyInput();
                Assert.False(vm.HasStatus);
            }
            Assert.Equal(0.5f, property.Value);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private static SerializedProperty Integer(object value) => new("Count", SerializedTypeEnum.Integer, value, "i32", null);
    private static SerializedProperty Float(object value) => new("Weight", SerializedTypeEnum.Float, value, "f32", null);
}