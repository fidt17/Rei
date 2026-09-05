using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.RectTransform;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

/// <summary>
/// Verifies RectTransform anchor preset matrix, matching tolerance, and command forwarding.
/// </summary>
[Trait("Area", "PropertyEditors")]
public sealed class RectTransformAnchorPresetTests
{
    /// <summary>
    /// Preset matrix covers fixed and stretched combinations on both axes.
    /// </summary>
    [Fact]
    public void PresetsCoverCompleteAnchorMatrix()
    {
        Assert.Equal(16, RectTransformAnchorPresetUtils.Presets.Count);
        Assert.Contains(RectTransformAnchorPresetUtils.Presets, x => x.DisplayName == "Top Left" && x.ButtonText == "TL");
        Assert.Contains(RectTransformAnchorPresetUtils.Presets, x => x.DisplayName == "Stretch Both" && x.ButtonText == "SS");
    }

    /// <summary>
    /// Matching accepts rounding noise below epsilon and rejects values at epsilon boundary.
    /// </summary>
    [Fact]
    public void MatchingUsesStrictEpsilonTolerance()
    {
        var center = Assert.Single(RectTransformAnchorPresetUtils.Presets, x => x.DisplayName == "Middle Center");

        Assert.Same(center, RectTransformAnchorPresetUtils.FindMatchingPreset(0.50009f, 0.5f, 0.5f, 0.5f));
        Assert.Null(RectTransformAnchorPresetUtils.FindMatchingPreset(0.5001f, 0.5f, 0.5f, 0.5f));
        Assert.False(RectTransformAnchorPresetUtils.IsStretch(0.5f, 0.50009f));
        Assert.True(RectTransformAnchorPresetUtils.IsStretch(0.5f, 0.5001f));
    }

    /// <summary>
    /// Preset view-model command forwards exact preset selected by caller.
    /// </summary>
    [Fact]
    public void ApplyCommandForwardsPreset()
    {
        var preset = RectTransformAnchorPresetUtils.Presets[0];
        RectTransformAnchorPreset? applied = null;
        var viewModel = new RectTransformAnchorPresetViewModel(preset, value => applied = value);

        viewModel.ApplyCommand.Execute(null);

        Assert.Same(preset, applied);
    }
}
