using System.Diagnostics;
using System.Text.Json;
using Avalonia.Headless.XUnit;
using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies.Services;
using Xunit.Abstractions;

namespace ReiEditor.Tests.Benchmarks;

[Trait("Suite", "Benchmark")]
public sealed class HierarchyInteractionBenchmarks(ITestOutputHelper output)
{
    private sealed class Rename : IEntityRenameCommand { public void Execute(EntityRenameCommandTarget target) { } }
    private sealed class Actions : ISelectedEntityActionService
    {
        public event Action<int>? RenameEntityRequested { add { } remove { } }
        public bool DeleteSelectedEntity() => false;
        public bool DuplicateSelectedEntity() => false;
        public bool RequestRenameSelectedEntity() => false;
    }
    private sealed class NodeFactory(SelectionService selection) : IFactory<HierarchyNodeViewModel>
    {
        public HierarchyNodeViewModel CreateInstance() => throw new NotSupportedException();
        public HierarchyNodeViewModel CreateInstance(params object[] parameters) => new((HierarchyNode<GameEntity>)parameters[0], new Rename(), new Actions(), selection);
    }

    [AvaloniaFact]
    public void MeasureHierarchyModelVmSelectionAndIncrementalMove()
    {
        if (Environment.GetEnvironmentVariable("REI_RUN_EDITOR_BENCHMARKS") != "1") return;
        var rows = new List<object>();
        foreach (var size in new[] { 100, 1000, 5000 })
        foreach (var nested in new[] { false, true })
        {
            var scene = new Scene("benchmark");
            var entities = Enumerable.Range(1, size).Select(id => new GameEntity(id, $"Entity {id}")).ToArray();
            foreach (var entity in entities) scene.AddEntity(entity);
            foreach (var entity in entities)
            {
                entity.Transform.SetParent(nested && entity.Id > 1 ? (entity.Id - 2) / 10 + 1 : 0);
                entity.Transform.SetOrder(entity.Id);
            }
            scene.RebuildHierarchy();
            var selection = new SelectionService(new TestEntityApi());
            var controller = new HierarchyNodeCollectionController(new NodeFactory(selection), _ => { });
            controller.SetHierarchy(scene.Hierarchy, new HashSet<int>());
            var selectionHandler = new HierarchySelectionHandler(selection, () => controller.GetAllNodes().ToArray(), () => controller.GetAllNodes().ToArray());
            selection.SelectionChanged.Subscribe(selectionHandler.HandleSelectionChanged, invoke: false);
            var sync = new ReiEditor.Models.Services.Entities.Sync.EngineSelectionSyncService(selection);
            Measure("model", scene.RebuildHierarchy);
            Measure("vm", () => controller.SetHierarchy(scene.Hierarchy, new HashSet<int>()));
            Measure("selection", () => { sync.ApplySelection(entities, new HashSet<int> { size }); sync.ApplySelection(entities, new HashSet<int> { 1 }); });
            Measure("incremental-move", () => { scene.MoveEntity(entities[^1], entities[0], 0); scene.MoveEntity(entities[^1], null, int.MaxValue); });
            controller.Dispose();
            Assert.Equal(size, scene.Entities.Count());

            void Measure(string stage, Action action)
            {
                action(); action();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var collections = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
                var samples = new List<double>();
                var bytes = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 12; i++)
                {
                    var start = Stopwatch.GetTimestamp();
                    action();
                    samples.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                }
                var allocated = (GC.GetAllocatedBytesForCurrentThread() - bytes) / 12;
                rows.Add(new { size, nested, stage, samples, allocatedBytes = allocated, collections = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - collections[i]).ToArray() });
                output.WriteLine($"{size} nested={nested} {stage}: median={samples.Order().ElementAt(6):F3}ms, max={samples.Max():F3}ms, bytes={allocated}");
            }
        }
        var path = Environment.GetEnvironmentVariable("REI_EDITOR_BENCHMARK_OUTPUT")!;
        File.WriteAllText(path, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
