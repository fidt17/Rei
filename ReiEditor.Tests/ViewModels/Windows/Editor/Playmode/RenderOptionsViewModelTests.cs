using ReiEditor.Models.Services.Render;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Playmode;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Playmode;

/// <summary>Verifies render option mapping, validation, state synchronization and disposal.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Playmode")]
public sealed class RenderOptionsViewModelTests
{
    private sealed class TestRenderApi : TestEngineApi
    {
        public List<(RenderMode Mode, bool Ui)> Requests { get; } = [];
        public override void ChangeRenderMode(RenderMode mode, bool renderUi) => Requests.Add((mode, renderUi));
    }

    /// <summary>Each supported option maps to its engine mode and preserves the UI rendering flag.</summary>
    [Theory]
    [InlineData("Shaded", RenderMode.Shaded)]
    [InlineData("Wireframe (Lines)", RenderMode.WireframeLines)]
    [InlineData("Wireframe (Points)", RenderMode.WireframePoints)]
    [InlineData("Depth", RenderMode.Depth)]
    [InlineData("Grayscale", RenderMode.Grayscale)]
    [InlineData("Inversion", RenderMode.Inversion)]
    [InlineData("BVH", RenderMode.BVH)]
    public void SelectionMapsToEngineMode(string name, RenderMode expected)
    {
        var api = new TestRenderApi();
        var settings = new RenderSettingsService { RenderMode = expected == RenderMode.Depth ? RenderMode.Shaded : RenderMode.Depth, IsUiRenderingEnabled = false };
        using var vm = new RenderModeSelectionViewModel(api, new TestEngineRunner(), settings);
        api.Requests.Clear();
        Assert.Contains(name, vm.Options);
        vm.SelectedRenderMode = name;
        vm.SelectedRenderMode = name;
        Assert.Equal(expected, settings.RenderMode);
        Assert.Equal((expected, false), Assert.Single(api.Requests));
    }

    /// <summary>Unknown options leave state untouched and unsupported saved modes fall back to Shaded.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-a-mode")]
    public void InvalidSelectionDoesNotChangeSavedMode(string invalid)
    {
        var api = new TestRenderApi();
        var settings = new RenderSettingsService { RenderMode = (RenderMode)999 };
        using var vm = new RenderModeSelectionViewModel(api, new TestEngineRunner(), settings);
        Assert.Equal("Shaded", vm.SelectedRenderMode);
        Assert.Equal(RenderMode.Shaded, settings.RenderMode);
        api.Requests.Clear();
        vm.SelectedRenderMode = invalid;
        Assert.Empty(api.Requests);
        Assert.Equal("Shaded", vm.SelectedRenderMode);
    }

    /// <summary>Engine state refreshes externally changed modes until disposal removes the subscription.</summary>
    [Fact]
    public void EngineActivationResynchronizesRenderSelectionUntilDisposed()
    {
        var api = new TestRenderApi();
        var settings = new RenderSettingsService();
        var runner = new TestEngineRunner();
        var vm = new RenderModeSelectionViewModel(api, runner, settings);
        settings.RenderMode = RenderMode.BVH;
        runner.Active.Value = true;
        Assert.True(vm.EngineActive);
        Assert.Equal("BVH", vm.SelectedRenderMode);
        vm.Dispose();
        api.Requests.Clear();
        runner.Active.Value = false;
        Assert.True(vm.EngineActive);
        Assert.Empty(api.Requests);
    }

    /// <summary>UI rendering writes once per toggle, resends on engine start and detaches on disposal.</summary>
    [Fact]
    public void UiTogglePreservesModeAndResendsOnEngineStart()
    {
        var api = new TestRenderApi();
        var settings = new RenderSettingsService { RenderMode = RenderMode.Depth, IsUiRenderingEnabled = false };
        var runner = new TestEngineRunner();
        var vm = new UIRenderOptionsViewModel(api, runner, settings);
        Assert.False(vm.RenderUi);
        Assert.Empty(api.Requests);
        vm.RenderUi = true;
        vm.RenderUi = true;
        Assert.Equal((RenderMode.Depth, true), Assert.Single(api.Requests));
        Assert.True(settings.IsUiRenderingEnabled);
        runner.Active.Value = true;
        Assert.Equal(new[] { (RenderMode.Depth, true), (RenderMode.Depth, true) }, api.Requests);
        vm.Dispose();
        runner.Active.Value = false;
        runner.Active.Value = true;
        Assert.Equal(2, api.Requests.Count);
    }
}
