using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Tests.Infrastructure.Headless;
using Autofac;
using ReiEditor.Models.EditorApp.Project.Commands.Assets;
using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Startup.Scopes.Editor.Modules;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Commands;
using ReiEditor.ViewModels.Windows.Editor.Commands.Entities;

namespace ReiEditor.Tests.Startup;

[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Composition")]
[Trait("Area", "EditorCommands")]
public sealed class EditorCommandsModuleTests
{
    private sealed class NativeApi : TestEngineApi
    {
        public List<(bool Undo, ulong Id)> Calls { get; } = [];
        public override EditorActionReplayResult UndoEditorAction(ulong id) { Calls.Add((true, id)); return EditorActionReplayResult.Applied; }
        public override EditorActionReplayResult RedoEditorAction(ulong id) { Calls.Add((false, id)); return EditorActionReplayResult.Applied; }
    }

    private sealed class Context : IDisposable
    {
        public GameEntity Entity { get; } = new(1, "Before");
        public TestEntityManagementService Entities { get; } = new();
        public NativeApi Api { get; } = new();
        public TestLogger<NativeEditorHistoryRecorder> Logger { get; } = new();
        public IContainer Container { get; }
        public IEditorCommandService Commands => Container.Resolve<IEditorCommandService>();

        public Context()
        {
            Entities.OnRename = (entity, name) => entity.SetName(name);
            var builder = new ContainerBuilder();
            builder.RegisterModule<EditorCommandsModule>();
            builder.RegisterInstance(Entities).As<IEntityManagementService>();
            builder.RegisterInstance(new TestSceneManagementService(Entity)).As<ISceneManagementService>();
            builder.RegisterInstance(new TestEngineRunner()).As<IEngineRunner>();
            builder.RegisterGeneric(typeof(TestLogger<>)).As(typeof(ILogger<>));
            builder.RegisterInstance(Api).As<IEngineApi>();
            builder.RegisterInstance(new TestEngineNativeAccess(Api)).As<IEngineNativeAccess>();
            builder.RegisterInstance(Logger).As<ILogger<NativeEditorHistoryRecorder>>();
            Container = builder.Build();
        }

        public IEntityRenameCommand Rename() => Container.Resolve<IEntityRenameCommand>();
        public void Dispose() => Container.Dispose();
    }

    [Fact]
    public async Task ResolvedRenameRecordsExactlyOnceAndReplayDoesNotRecordAgain()
    {
        using var c = new Context();
        var rename = Assert.IsType<EntityRenameCommandDecorator>(c.Rename());
        Assert.Same(c.Commands, c.Commands);
        await rename.ExecuteAsync(new(c.Entity, "After"));
        Assert.Equal("After", c.Entity.Name);
        Assert.True(c.Commands.CanUndo);
        Assert.False(c.Commands.CanRedo);

        await c.Commands.UndoAsync();
        Assert.Equal("Before", c.Entity.Name);
        Assert.False(c.Commands.CanUndo);
        Assert.True(c.Commands.CanRedo);
        await c.Commands.UndoAsync();
        Assert.Equal(2, c.Entities.RenameCalls.Count);

        await c.Commands.RedoAsync();
        Assert.Equal("After", c.Entity.Name);
        Assert.True(c.Commands.CanUndo);
        Assert.False(c.Commands.CanRedo);
        await c.Commands.RedoAsync();
        Assert.Equal(3, c.Entities.RenameCalls.Count);
        await c.Commands.UndoAsync();
        Assert.False(c.Commands.CanUndo);
        Assert.Equal("Before", c.Entity.Name);
        Assert.Equal(new[] { "After", "Before", "After", "Before" }, c.Entities.RenameCalls.Select(call => call.Name));
    }

    [Fact]
    public async Task SeparateResolvedCommandsShareOneHistory()
    {
        using var c = new Context();
        var first = c.Rename();
        var second = c.Rename();
        Assert.NotSame(first, second);
        await first.ExecuteAsync(new(c.Entity, "One"));
        await second.ExecuteAsync(new(c.Entity, "Two"));
        await c.Commands.UndoAsync();
        Assert.Equal("One", c.Entity.Name);
        await c.Commands.UndoAsync();
        Assert.Equal("Before", c.Entity.Name);
        Assert.False(c.Commands.CanUndo);
        await c.Commands.RedoAsync();
        await c.Commands.RedoAsync();
        Assert.Equal("Two", c.Entity.Name);
        Assert.False(c.Commands.CanRedo);
    }

    [Fact]
    public async Task DecoratedNoopAndFailurePreserveRedoAndReleaseBusy()
    {
        using var c = new Context();
        var rename = c.Rename();
        await rename.ExecuteAsync(new(c.Entity, "After"));
        await c.Commands.UndoAsync();
        Assert.False((await rename.ExecuteAsync(new(c.Entity, "Before"))).Changed);
        Assert.True(c.Commands.CanRedo);
        c.Entities.OnRename = (_, _) => throw new InvalidOperationException("rename failed");
        await Assert.ThrowsAsync<InvalidOperationException>(() => rename.ExecuteAsync(new(c.Entity, "Unapplied")));
        Assert.False(c.Commands.IsBusy);
        Assert.False(c.Commands.CanUndo);
        Assert.True(c.Commands.CanRedo);
        Assert.Equal("Before", c.Entity.Name);
        c.Entities.OnRename = (entity, name) => entity.SetName(name);
        await c.Commands.RedoAsync();
        Assert.Equal("After", c.Entity.Name);
    }

    [Fact]
    public void CommandServiceResolvesWithoutSceneOrEngineDependencies()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<EditorCommandService>().As<IEditorCommandService>();
        using var container = builder.Build();
        var commands = container.Resolve<IEditorCommandService>();
        Assert.False(commands.IsBusy);
        Assert.False(commands.CanUndo);
        Assert.False(commands.CanRedo);
    }

    [AvaloniaFact]
    public async Task NativeCallbacksAndRenameUseOneHistoryThroughDi()
    {
        using var c = new Context();
        var commands = c.Commands;
        var rename = c.Rename();
        Assert.Equal(1, c.Api.ActionRecordedSubscribers);
        Assert.Same(c.Container.Resolve<NativeEditorHistoryRecorder>(), c.Container.Resolve<NativeEditorHistoryRecorder>());
        await rename.ExecuteAsync(new(c.Entity, "First"));
        const ulong ACTION_ID = (1UL << 63) + 17;
        c.Api.EmitActionRecorded(ACTION_ID);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(c.Api.Calls);
        await rename.ExecuteAsync(new(c.Entity, "Last"));

        await commands.UndoAsync();
        Assert.Equal("First", c.Entity.Name);
        Assert.Empty(c.Api.Calls);
        await commands.UndoAsync();
        Assert.Equal(new[] { (true, ACTION_ID) }, c.Api.Calls);
        Assert.Equal("First", c.Entity.Name);
        await commands.UndoAsync();
        Assert.Equal("Before", c.Entity.Name);
        Assert.False(commands.CanUndo);

        await commands.RedoAsync();
        Assert.Equal("First", c.Entity.Name);
        await commands.RedoAsync();
        Assert.Equal(new[] { (true, ACTION_ID), (false, ACTION_ID) }, c.Api.Calls);
        await commands.RedoAsync();
        Assert.Equal("Last", c.Entity.Name);
        Dispatcher.UIThread.RunJobs();
        Assert.False(commands.CanRedo);
        Assert.Empty(c.Logger.Entries);
    }

    [AvaloniaFact]
    public async Task BackgroundCallbacksArePostedToUiAndRecordedOnceInOrder()
    {
        using var c = new Context();
        var worker = new Thread(() =>
        {
            c.Api.EmitActionRecorded(7);
            c.Api.EmitActionRecorded(9);
        });
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        Assert.False(c.Commands.CanUndo); // The UI has not processed the callbacks yet.
        Dispatcher.UIThread.RunJobs();
        Assert.True(c.Commands.CanUndo);
        await c.Commands.UndoAsync();
        await c.Commands.UndoAsync();
        Assert.False(c.Commands.CanUndo);
        await c.Commands.RedoAsync();
        await c.Commands.RedoAsync();
        Dispatcher.UIThread.RunJobs();
        Assert.False(c.Commands.CanRedo);
        Assert.Equal(new[] { (true, 9UL), (true, 7UL), (false, 7UL), (false, 9UL) }, c.Api.Calls);
        Assert.Empty(c.Logger.Entries);
    }

    [AvaloniaFact]
    public void DisposeUnsubscribesAndIgnoresAlreadyPostedCallbacks()
    {
        using var c = new Context();
        var commands = c.Commands;
        Assert.Equal(1, c.Api.ActionRecordedSubscribers);
        c.Api.EmitActionRecorded(7);
        c.Dispose();
        Assert.Equal(0, c.Api.ActionRecordedSubscribers);
        c.Api.EmitActionRecorded(9);
        Dispatcher.UIThread.RunJobs();
        Assert.False(commands.CanUndo);
        Assert.Empty(c.Api.Calls);
        Assert.Empty(c.Logger.Entries);
    }

    [AvaloniaFact]
    public void FailedRegistrationIsLogged()
    {
        using var c = new Context();
        ((IDisposable)c.Commands).Dispose();
        c.Api.EmitActionRecorded(7);
        Dispatcher.UIThread.RunJobs();
        Assert.IsType<ObjectDisposedException>(Assert.Single(c.Logger.Entries).Exception);
        Assert.Empty(c.Api.Calls);
    }

    [Fact]
    public void ModuleOwnsEditorCommandRegistrations()
    {
        using var c = new Context();
        foreach (var type in new[]
        {
            typeof(IEditorCommandService), typeof(NativeEditorHistoryRecorder), typeof(IEntityRenameCommand),
            typeof(ISelectedEntityDeleteCommand), typeof(ISelectedEntityDuplicateCommand), typeof(ISelectedEntityRenameCommand),
            typeof(IProjectAssetDeleteCommand), typeof(IProjectAssetDuplicateCommand), typeof(IProjectAssetMoveCommand), typeof(IProjectAssetRenameCommand),
            typeof(CreateSceneEntityCommand), typeof(SaveProjectCommand)
        }) Assert.True(c.Container.IsRegistered(type), type.Name);
    }
}
