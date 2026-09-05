using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.EditorApp.Refresh;
using ReiEditor.Models.Services.Entities;
using ReiEditor.ViewModels.Windows.Editor.Monitor;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

/// <summary>
/// Verifies monitor drawer routing plus selection, refresh, synchronization, and owner lifetime.
/// </summary>
[Trait("Area", "Monitor")]
public sealed class MonitorDrawerUtilsTests
{
    /// <summary>
    /// Publishes monitor selections and records entity selection requested by consumers.
    /// </summary>
    private sealed class TestSelectionService : ISelectionService
    {
        public ReiEditor.Utils.Common.Observable<ISelectable?> Active { get; } = new(null);
        public ReiEditor.Utils.Common.Observable<IReadOnlyCollection<ISelectable>> Changed { get; } = new([]);
        public ReiEditor.Utils.Common.IObservable<ISelectable?> ActiveSelection => Active;
        public ReiEditor.Utils.Common.IObservable<IReadOnlyCollection<ISelectable>> SelectionChanged => Changed;
        public IReadOnlyCollection<ISelectable> SelectedItems => [];

        public void Select(ISelectable selectable) => Active.Value = selectable;
        public void Select(GameEntity e, bool sendToEngine = true) => throw new NotSupportedException();
        public void AddSelection(GameEntity e, bool sendToEngine = true) => throw new NotSupportedException();
        public void Deselect(ISelectable selectable, bool sendToEngine = true) => throw new NotSupportedException();
        public void Deselect(GameEntity e, bool sendToEngine = true) => throw new NotSupportedException();
        public bool IsSelected(ISelectable selectable) => ReferenceEquals(Active.Value, selectable);
        public bool IsEntitySelected(GameEntity e) => false;
        public IEntitySelectable? GetEntitySelectable(GameEntity e) => null;
        public void SetSelection(IReadOnlyCollection<ISelectable> selectables, ISelectable? primarySelection = null, bool sendToEngine = true) => throw new NotSupportedException();
        public void AddSelection(ISelectable selectable, bool sendToEngine = true) => throw new NotSupportedException();
        public void RemoveSelection(ISelectable selectable, bool sendToEngine = true) => throw new NotSupportedException();
        public void ToggleSelection(ISelectable selectable, bool sendToEngine = true) => throw new NotSupportedException();
        public void ResetSelection(bool sendToEngine = true) => Active.Value = null;
        public void RegisterSelectable(ISelectable selectable) { }
        public void UnregisterSelectable(ISelectable selectable) { }
    }

    /// <summary>
    /// Publishes explicit monitor refresh events.
    /// </summary>
    private sealed class TestEditorRefreshService : IEditorRefreshService
    {
        public event Action? RefreshedEvent;
        public void NotifyRefreshed() => RefreshedEvent?.Invoke();
    }

    /// <summary>
    /// Supplies selection contract without asset metadata.
    /// </summary>
    private sealed class TestSelectable : ISelectable
    {
        public void Select() { }

        public void Deselect() { }
    }

    /// <summary>
    /// Supplies asset routing fields without project-browser dependencies.
    /// </summary>
    private sealed class TestAssetSelectable(string path, bool supported) : IAssetSelectable
    {
        public string AssetId => "asset-id";
        public string AssetName => Path.GetFileName(path);
        public string AssetPath => path;
        public bool IsAssetSupportedInMonitor => supported;

        public void Select() { }

        public void Deselect() { }
    }

    /// <summary>
    /// Empty and non-asset selections produce no drawer or entity synchronization target.
    /// </summary>
    [Fact]
    public void NonAssetSelectionProducesNoDrawer()
    {
        var drawer = TestCreateDrawer(new TestSelectable(), out var entity);

        Assert.Null(drawer);
        Assert.Null(entity);
    }

    /// <summary>
    /// Unsupported and supported non-material assets both use generic asset drawer.
    /// </summary>
    [Fact]
    public void NonMaterialSelectionProducesAssetDrawer()
    {
        foreach (var (path, supported) in new[] { ("script.cpp", false), ("mesh.obj", true) })
        {
            var drawer = TestCreateDrawer(new TestAssetSelectable(path, supported), out var entity);

            Assert.IsType<AssetMonitorDrawerViewModel>(drawer);
            Assert.Null(entity);
        }
    }

    /// <summary>
    /// Asset selection and refresh choose and recreate current generic drawer, while disposal stops later selection reactions.
    /// </summary>
    [Fact]
    public void MonitorTracksSelectionRefreshAndSubscriptionLifetime()
    {
        var selection = new TestSelectionService();
        var refresh = new TestEditorRefreshService();
        var viewModel = TestCreateMonitor(selection, refresh);
        selection.Active.Value = new TestAssetSelectable("mesh.obj", supported: true);
        var selectedDrawer = Assert.IsType<AssetMonitorDrawerViewModel>(viewModel.Drawer);

        refresh.NotifyRefreshed();
        Assert.IsType<AssetMonitorDrawerViewModel>(viewModel.Drawer);
        Assert.NotSame(selectedDrawer, viewModel.Drawer);
        var finalDrawer = viewModel.Drawer;

        viewModel.Dispose();
        selection.Active.Value = null;
        refresh.NotifyRefreshed();
        Assert.Same(finalDrawer, viewModel.Drawer);
    }

    private static BaseMonitorDrawer? TestCreateDrawer(ISelectable? selection, out ReiEditor.Models.Services.Entities.GameEntity? entity)
        => MonitorDrawerUtils.CreateDrawer(selection, null!, null!, null!, null!, null!, null!, null!, null!, out entity);

    private static MonitorWindowViewModel TestCreateMonitor(TestSelectionService selection, TestEditorRefreshService refresh)
        => new(selection, refresh, null!, null!, null!, null!, null!, null!, null!, null!, null!);

}
