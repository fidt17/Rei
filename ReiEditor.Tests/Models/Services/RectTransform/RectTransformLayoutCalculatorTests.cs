using ReiEditor.Models.Services.RectTransform;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.RectTransform;

namespace ReiEditor.Tests.Models.Services.RectTransform;

/// <summary>Verifies anchor and pivot calculations using hand-computed rectangles without rendering.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "RectTransformMath")]
public sealed class RectTransformLayoutCalculatorTests
{
    /// <summary>Fixed, stretched, and zero-sized parents produce independently calculated rectangle coordinates.</summary>
    [Theory]
    [InlineData(200f, 100f, 0.5f, 0.5f, 40f, 20f, 90f, 35f, 130f, 55f)]
    [InlineData(200f, 100f, 0f, 1f, -20f, -10f, 20f, 0f, 200f, 90f)]
    [InlineData(0f, 0f, 0.5f, 0.5f, 40f, 20f, -10f, -15f, 30f, 5f)]
    public void RectCoordinatesMatchAnchorSpanAndPivot(float width, float height, float min, float max, float sizeX, float sizeY, float left, float bottom, float right, float top)
    {
        var data = new RectTransformLayoutData(new(min, min), new(max, max), new(0.5f, 0.5f), new(10, -5), new(sizeX, sizeY));
        var rect = RectTransformLayoutCalculator.CalculateRect(new(width, height), data);

        Assert.Equal(new RectTransformRect(left, bottom, right, top), rect);
    }

    /// <summary>Inverse layout preserves a known rectangle when anchors and pivot change.</summary>
    [Theory]
    [InlineData(0f, 0f, 0f, 0f)]
    [InlineData(0.5f, 0.5f, 0.5f, 0.5f)]
    [InlineData(0f, 1f, 1f, 1f)]
    public void InverseLayoutPreservesRectAcrossAnchorsAndPivots(float min, float max, float pivotX, float pivotY)
    {
        var parent = new RectTransformVector2(200, 100);
        var expected = new RectTransformRect(-10, 20, 70, 60);
        var values = RectTransformLayoutCalculator.CalculateValuesForRect(parent, expected, min, min, max, max, pivotX, pivotY);
        var actual = RectTransformLayoutCalculator.CalculateRect(parent, min, min, max, max, pivotX, pivotY,
            values.AnchoredPositionX, values.AnchoredPositionY, values.SizeDeltaX, values.SizeDeltaY);

        Assert.Equal(expected, actual);
        Assert.Equal(80 - 200 * (max - min), values.SizeDeltaX);
        Assert.Equal(40 - 100 * (max - min), values.SizeDeltaY);
    }

    /// <summary>Different anchors and pivots on each axis produce independently calculated forward and inverse values.</summary>
    [Fact]
    public void AsymmetricAxesKeepIndependentAnchorsAndPivots()
    {
        var parent = new RectTransformVector2(200, 100);
        var data = new RectTransformLayoutData(new(0.25f, 0.5f), new(0.75f, 1f), new(0.25f, 0.75f), new(10, -5), new(40, 20));
        var expected = new RectTransformRect(50, 30, 190, 100);

        Assert.Equal(expected, RectTransformLayoutCalculator.CalculateRect(parent, data));

        var values = RectTransformLayoutCalculator.CalculateValuesForRect(parent, expected, 0.25f, 0.5f, 0.75f, 1f, 0.25f, 0.75f);
        Assert.Equal(10f, values.AnchoredPositionX);
        Assert.Equal(-5f, values.AnchoredPositionY);
        Assert.Equal(40f, values.SizeDeltaX);
        Assert.Equal(20f, values.SizeDeltaY);
    }

    /// <summary>Preset lookup recognizes centered and stretched anchors, tolerates small drift, and rejects custom anchors.</summary>
    [Fact]
    public void PresetLookupUsesAnchorTolerance()
    {
        var center = RectTransformAnchorPresetUtils.FindMatchingPreset(0.5f, 0.5f, 0.5f, 0.5f);
        Assert.NotNull(center);
        Assert.True(RectTransformAnchorPresetUtils.IsMatching(center, 0.50001f, 0.5f, 0.5f, 0.5f));
        Assert.False(RectTransformAnchorPresetUtils.IsMatching(center, 0.51f, 0.5f, 0.5f, 0.5f));
        Assert.NotNull(RectTransformAnchorPresetUtils.FindMatchingPreset(0, 0, 1, 1));
        Assert.Null(RectTransformAnchorPresetUtils.FindMatchingPreset(0.2f, 0.3f, 0.7f, 0.8f));
        Assert.False(RectTransformAnchorPresetUtils.IsStretch(0.5f, 0.50001f));
        Assert.True(RectTransformAnchorPresetUtils.IsStretch(0, 1));
    }
}
