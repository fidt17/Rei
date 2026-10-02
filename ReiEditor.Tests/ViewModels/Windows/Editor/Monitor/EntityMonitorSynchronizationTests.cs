using System.Diagnostics.CodeAnalysis;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Monitor")]
public sealed class EntityMonitorSynchronizationTests
{
    private sealed class Factory<T>(Func<object[], T> create) : IFactory<T> where T : class
    {
        public T CreateInstance() => create([]);
        public T CreateInstance(params object[] parameters) => create(parameters);
    }

    private sealed class Registry : IBehaviourRegistry
    {
        public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours { get; } = new Dictionary<int, BehaviourAssetInfo>
        {
            [1] = new("test", "TestComponent", 1, new ObjectFile<string>("", "test.h"), new(), [], "test.h")
        };
        public bool TryGetById(int id, [NotNullWhen(true)] out BehaviourAssetInfo? behaviour) => Behaviours.TryGetValue(id, out behaviour);
        public int? GetIdByName(string name) => Behaviours.Values.FirstOrDefault(info => info.ObjectName == name)?.BehaviourId;
        public int AllocateBehaviourId() => throw new NotSupportedException();
        public Task RefreshBehaviours() => throw new NotSupportedException();
    }

    private sealed class NoCustomProperties : IRectTransformCustomPropertiesProvider
    {
        public IEnumerable<BaseViewModel> CreateProperties(GameEntity entity, BehaviourComponent component) => [];
        public bool OwnsSerializedProperty(BehaviourComponent component, string propertyName) => false;
    }

    [AvaloniaFact]
    public async Task BackgroundComponentChangesUpdateMonitorOnUiThread()
    {
        var entity = new GameEntity(1, "Name");
        using var vm = CreateDrawer(entity);
        var threads = new List<bool>();
        vm.Elements.CollectionChanged += (_, _) => threads.Add(Dispatcher.UIThread.CheckAccess());
        vm.BehaviourSelection.CollectionChanged += (_, _) => threads.Add(Dispatcher.UIThread.CheckAccess());
        var component = new BehaviourComponent(1);
        await Task.Run(() => entity.AddBehaviour(component));
        Dispatcher.UIThread.RunJobs();
        Assert.Same(component, Assert.Single(vm.Elements.OfType<BehaviourComponentDrawerViewModel>()).BehaviourComponent);
        await Task.Run(() => entity.DeleteBehaviour(component));
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(vm.Elements.OfType<BehaviourComponentDrawerViewModel>());
        Assert.NotEmpty(threads);
        Assert.All(threads, isUiThread => Assert.True(isUiThread));
    }

    [AvaloniaFact]
    public void QueuedComponentChangeIsIgnoredAfterMonitorCloses()
    {
        var entity = new GameEntity(1, "Name");
        var vm = CreateDrawer(entity);
        using (Dispatcher.UIThread.DisableProcessing())
        {
            var worker = new Thread(() => entity.AddBehaviour(new BehaviourComponent(1)));
            worker.Start();
            Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
            vm.Dispose();
        }
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(vm.Elements);
        Assert.Empty(vm.BehaviourSelection);
    }

    [AvaloniaFact]
    public void QueuedAddAndRemoveDoNotLeaveStaleComponentDrawer()
    {
        var entity = new GameEntity(1, "Name");
        using var vm = CreateDrawer(entity);
        using (Dispatcher.UIThread.DisableProcessing())
        {
            var worker = new Thread(() =>
            {
                var component = new BehaviourComponent(1);
                entity.AddBehaviour(component);
                entity.DeleteBehaviour(component);
            });
            worker.Start();
            Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        }
        Dispatcher.UIThread.RunJobs();
        Assert.Single(vm.Elements);
        Assert.Empty(vm.Elements.OfType<BehaviourComponentDrawerViewModel>());
        Assert.Single(vm.BehaviourSelection);
    }

    private static EntityMonitorDrawerViewModel CreateDrawer(GameEntity entity)
    {
        var registry = new Registry();
        return new(entity,
            new Factory<EntityInfoComponentDrawerViewModel>(args => new((GameEntity)args[0], null!)),
            new Factory<BehaviourComponentDrawerViewModel>(args => new((GameEntity)args[0], (BehaviourComponent)args[1], registry, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, new NoCustomProperties())),
            registry, null!);
    }
}
