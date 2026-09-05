using ReiEditor.Models.EditorApp.ViewportGrid;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Playmode;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Playmode;

/// <summary>Verifies grid option synchronization, selection routing and engine state subscription lifetime.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Playmode")]
public sealed class EditorGridOptionsViewModelTests
{
    private sealed class TestGrid : IViewportGridService
    {
        public ViewportGridSettings Settings { get; } = new() { RenderXZ = false, RenderXY = true, Opacity = 0.5f };
        public List<(string Name, float Value)> Requests { get; } = [];
        public ViewportGridSettings GetCurrentSettings() => Settings;
        public void EnableXZGrid(bool value)
        {
            Requests.Add(("XZ", value ? 1 : 0));
            Settings.RenderXZ = value;
            if (value) { Settings.RenderXY = false; Settings.RenderYZ = false; }
        }
        public void EnableXYGrid(bool value)
        {
            Requests.Add(("XY", value ? 1 : 0));
            Settings.RenderXY = value;
            if (value) { Settings.RenderXZ = false; Settings.RenderYZ = false; }
        }
        public void EnableYZGrid(bool value)
        {
            Requests.Add(("YZ", value ? 1 : 0));
            Settings.RenderYZ = value;
            if (value) { Settings.RenderXZ = false; Settings.RenderXY = false; }
        }
        public void SetOpacity(float value) { Requests.Add(("Opacity", value)); Settings.Opacity = value; }
    }

    /// <summary>Changing a plane re-reads service state for other toggles and unchanged values avoid another request.</summary>
    [Fact]
    public void PlaneSelectionReflectsServiceMutualExclusion()
    {
        var grid = new TestGrid();
        var runner = new TestEngineRunner();
        using var vm = new EditorGridOptionsViewModel(grid, runner);
        Assert.True(vm.XY);
        Assert.False(vm.XZ);
        Assert.Equal(0.5f, vm.Opacity);
        grid.Requests.Clear();
        vm.YZ = true;
        Assert.True(vm.YZ);
        Assert.False(vm.XY);
        Assert.False(vm.XZ);
        Assert.Contains(("YZ", 1f), grid.Requests);
        var count = grid.Requests.Count;
        vm.YZ = true;
        Assert.Equal(count, grid.Requests.Count);
        vm.Opacity = 0.8f;
        Assert.Equal(0.8f, grid.Settings.Opacity);
        Assert.Equal(("Opacity", 0.8f), grid.Requests[^1]);
        runner.Active.Value = true;
        Assert.True(vm.EngineActive);
        vm.Dispose();
        runner.Active.Value = false;
        Assert.True(vm.EngineActive);
    }
}
