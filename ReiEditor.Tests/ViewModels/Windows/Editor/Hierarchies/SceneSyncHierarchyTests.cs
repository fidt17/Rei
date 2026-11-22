using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using ReiEditor.Models.EditorApp.Refresh;
using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.SceneSync;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Entities.Sync;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies.Services;
using ReiEditor.ViewModels.Windows.Editor.Monitor;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;
using ReiEditor.Views.Windows.Editor.Hierarchies;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Hierarchies;

[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "SceneSync")]
public sealed class SceneSyncHierarchyTests
{
    private sealed class Factory<T>(Func<object[], T> create) : IFactory<T> where T : class
    {
        public T CreateInstance() => create([]);
        public T CreateInstance(params object[] parameters) => create(parameters);
    }

    private sealed class Source : IEngineSceneSyncEventSource
    {
        public event Action<SceneSyncPacket>? Received;
        public event Action? ConnectionReset { add { } remove { } }
        public bool IsConnected => true;
        public void Connect() { }
        public void Disconnect() { }
        public void RequestSnapshot() { }
        public void Send(SceneSyncPacket packet) => Received?.Invoke(packet);
    }

    private sealed class Rename : IEntityRenameCommand
    {
        public Task<EntityRenameCommandResult> ExecuteAsync(EntityRenameCommandTarget target) => Task.FromResult(new EntityRenameCommandResult(target.Entity.Name, target.Entity.Name));
    }

    private sealed class Actions : ISelectedEntityActionService
    {
        public event Action<int>? RenameEntityRequested { add { } remove { } }
        public bool DeleteSelectedEntity() => throw new NotSupportedException();
        public bool DuplicateSelectedEntity() => throw new NotSupportedException();
        public bool RequestRenameSelectedEntity() => throw new NotSupportedException();
    }

    private sealed class Refresh : IEditorRefreshService
    {
        public event Action? RefreshedEvent;
        public void NotifyRefreshed() => RefreshedEvent?.Invoke();
    }

    private sealed class EntitySync : IEntitySyncService
    {
        public void UpdateEntityState(GameEntity entity) { }
    }

    private sealed class Registry : IBehaviourRegistry
    {
        public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours { get; } = new Dictionary<int, BehaviourAssetInfo>();
        public bool TryGetById(int id, [NotNullWhen(true)] out BehaviourAssetInfo? behaviour) => Behaviours.TryGetValue(id, out behaviour);
        public int? GetIdByName(string name) => null;
        public int AllocateBehaviourId() => throw new NotSupportedException();
        public Task RefreshBehaviours() => throw new NotSupportedException();
    }

    private sealed class Fixture : IDisposable
    {
        public Scene Scene { get; } = new("scene");
        public TestEntityApi Api { get; } = new();
        public SelectionService Selection { get; }
        public HierarchyNodeCollectionController Nodes { get; }
        public MonitorWindowViewModel Monitor { get; }
        public int CreatedNodes { get; private set; }
        public int CreatedDrawers { get; private set; }
        private readonly Source _source = new();
        private readonly SceneSyncService _sync;
        private readonly HierarchySelectionHandler _highlights;

        public Fixture()
        {
            Selection = new(Api);
            Nodes = new(new Factory<HierarchyNodeViewModel>(args =>
            {
                CreatedNodes++;
                return new((HierarchyNode<GameEntity>)args[0], new Rename(), new Actions(), Selection);
            }), _ => { });
            Nodes.SetHierarchy(Scene.Hierarchy, new HashSet<int>());
            _highlights = new(Selection, () => Nodes.GetAllNodes().ToArray(), () => Nodes.GetAllNodes().ToArray());
            Selection.SelectionChanged.Subscribe(_highlights.HandleSelectionChanged);
            var scenes = new TestSceneManagementService();
            scenes.Scene.Value = Scene;
            _sync = new(_source, scenes, new EngineSelectionSyncService(Selection), new TestLogger<SceneSyncService>(), new TestEntitySyncService());
            Monitor = new(Selection, new Refresh(), new Factory<EntityMonitorDrawerViewModel>(args =>
            {
                CreatedDrawers++;
                return new((GameEntity)args[0],
                    new Factory<EntityInfoComponentDrawerViewModel>(values => new((GameEntity)values[0], new Rename(), new TestLogger<EntityInfoComponentDrawerViewModel>())),
                    new Factory<BehaviourComponentDrawerViewModel>(_ => throw new NotSupportedException()), new Registry(), null!);
            }), null!, null!, null!, null!, null!, null!, null!, null!, new EntitySync());
        }

        public void Send(SceneEntityState[] entities, bool snapshot = false, int[]? removed = null, int[]? selected = null)
        {
            _source.Send(new() { IsSnapshot = snapshot, Entities = entities, RemovedEntityIds = removed ?? [], SelectedEntityIds = selected ?? [3, 4] });
            Dispatcher.UIThread.RunJobs();
        }

        public HierarchyNodeViewModel Node(int id) => Nodes.FindByEntityId(id)!;
        public void Dispose()
        {
            _sync.Dispose();
            Monitor.Dispose();
            Selection.SelectionChanged.Unsubscribe(_highlights.HandleSelectionChanged);
            Nodes.Dispose();
        }
    }

    [AvaloniaFact]
    public void DeltaAndSnapshotPreserveSelectedSubtreeAndMonitorUntilEntityIsRemoved()
    {
        using var fixture = new Fixture();
        fixture.Send([State(1), State(2, order: 1), State(3, 1), State(4, 3)], snapshot: true);
        var hierarchy = fixture.Scene.Hierarchy;
        var originals = fixture.Nodes.GetAllNodes().ToDictionary(node => node.Node.Content.Id);
        foreach (var node in originals.Values) node.Expanded.Value = true;
        var drawer = Assert.IsType<EntityMonitorDrawerViewModel>(fixture.Monitor.Drawer);
        drawer.SearchField.Query.Value = "keep search";
        var primary = fixture.Selection.ActiveSelection.Value;
        var published = 0;
        fixture.Selection.SelectionChanged.Subscribe(_ => published++, invoke: false);

        fixture.Send([new(3, "renamed", 2, 7)]);
        fixture.Send([State(2, order: 1), new(3, "renamed", 2, 7), State(4, 3)], snapshot: true);

        Assert.Same(hierarchy, fixture.Scene.Hierarchy);
        foreach (var id in new[] { 2, 3, 4 })
        {
            Assert.Same(originals[id], fixture.Node(id));
            Assert.Same(originals[id].Node, hierarchy.GetNode(fixture.Scene.GetById(id)!));
            Assert.True(fixture.Node(id).Expanded.Value);
        }
        Assert.Equal("renamed", fixture.Node(3).Name.Value);
        Assert.Same(fixture.Node(3), Assert.Single(fixture.Node(2).ChildNodes));
        Assert.Same(fixture.Node(4), Assert.Single(fixture.Node(3).ChildNodes));
        Assert.Same(primary, fixture.Selection.ActiveSelection.Value);
        Assert.Same(drawer, fixture.Monitor.Drawer);
        Assert.Equal("keep search", drawer.SearchField.Query.Value);
        Assert.Equal(0, published);
        Assert.Equal(4, fixture.CreatedNodes);
        Assert.Equal(1, fixture.CreatedDrawers);
        Assert.True(fixture.Node(3).Selected.Value);
        Assert.True(fixture.Node(4).Selected.Value);
        Assert.Empty(fixture.Api.Selections);

        fixture.Send([], removed: [3, 4], selected: []);
        Assert.Null(fixture.Monitor.Drawer);
        Assert.Empty(drawer.Elements);
        Assert.Null(fixture.Nodes.FindByEntityId(3));
        Assert.Null(fixture.Selection.GetEntitySelectable(originals[3].Node.Content));
    }

    [AvaloniaFact]
    public void ReparentAndReorderReusePhysicalViewsAndTheirCommandSubscriptions()
    {
        using var fixture = new Fixture();
        fixture.Send([State(1), State(2, order: 1), State(3, 1), State(4, 3)], snapshot: true);
        foreach (var node in fixture.Nodes.GetAllNodes()) node.Expanded.Value = true;
        var styles = new StyleInclude(new Uri("avares://ReiEditor/")) { Source = new Uri("avares://ReiEditor/Views/Resources/Styles.axaml") };
        var theme = new FluentTheme();
        Application.Current!.Styles.Add(theme);
        Application.Current.Styles.Add(styles);
        var items = new ItemsControl { ItemsSource = fixture.Nodes.Nodes, ItemTemplate = new HierarchyNodeTemplate() };
        var window = new Window { Content = items, Width = 400, Height = 500 };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var originals = Views(items);
            Assert.Equal(4, originals.Count);
            fixture.Send([State(3, 2)]);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            AssertViewsUnchanged(originals, Views(items));
            fixture.Node(3).StartRenameCommand.Execute(null);
            Assert.True(originals[3].FindControl<TextBox>("NameTextBox")!.IsVisible);
            fixture.Send([State(1, order: 1), State(2)]);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            AssertViewsUnchanged(originals, Views(items));
        }
        finally
        {
            window.Close();
            Application.Current.Styles.Remove(styles);
            Application.Current.Styles.Remove(theme);
        }
    }

    [AvaloniaFact]
    public void ParentChildSwapAndSiblingOrderKeepExistingNodes()
    {
        using var fixture = new Fixture();
        fixture.Send([State(1), State(2, 1), State(3, 2), State(4, 2, 1)], snapshot: true, selected: [3]);
        var originals = fixture.Nodes.GetAllNodes().ToDictionary(node => node.Node.Content.Id);
        var actions = new List<NotifyCollectionChangedAction>();
        fixture.Node(2).ChildNodes.CollectionChanged += (_, args) => actions.Add(args.Action);

        fixture.Send([State(1, 2), State(2), State(3, 2, 1), State(4, 2)], selected: [3]);

        Assert.Same(originals[2], Assert.Single(fixture.Nodes.Nodes));
        Assert.Equal(new[] { 1, 4, 3 }, fixture.Node(2).ChildNodes.Select(node => node.Node.Content.Id));
        foreach (var original in originals) Assert.Same(original.Value, fixture.Node(original.Key));
        Assert.Contains(NotifyCollectionChangedAction.Move, actions);
        Assert.Equal(4, fixture.CreatedNodes);
        Assert.Equal(1, fixture.CreatedDrawers);
    }

    [AvaloniaFact]
    public void InvalidParentsAreDetachedWithoutReplacingSurvivingNodes()
    {
        using var fixture = new Fixture();
        fixture.Send([State(1), State(2, 1), State(3, 2)], snapshot: true, selected: [3]);
        var originals = fixture.Nodes.GetAllNodes().ToDictionary(node => node.Node.Content.Id);
        fixture.Send([State(1, 3), State(2, 1), State(3, 2)], selected: [3]);
        Assert.Equal(3, fixture.Nodes.GetAllNodes().Count());
        Assert.Null(fixture.Node(1).Node.Parent);
        fixture.Send([State(3, 999)], selected: [3]);
        Assert.Null(fixture.Node(3).Node.Parent);
        Assert.Equal(0, fixture.Node(3).Node.Content.Transform.Parent);
        foreach (var original in originals) Assert.Same(original.Value, fixture.Node(original.Key));
        Assert.Equal(1, fixture.CreatedDrawers);
    }

    private static SceneEntityState State(int id, int parent = 0, int order = 0) => new(id, $"entity {id}", parent, order);
    private static Dictionary<int, HierarchyNode> Views(Control control) => control.GetVisualDescendants().OfType<HierarchyNode>().ToDictionary(view => ((HierarchyNodeViewModel)view.DataContext!).Node.Content.Id);
    private static void AssertViewsUnchanged(Dictionary<int, HierarchyNode> originals, Dictionary<int, HierarchyNode> current)
    {
        Assert.Equal(originals.Count, current.Count);
        foreach (var original in originals) Assert.Same(original.Value, current[original.Key]);
    }
}
