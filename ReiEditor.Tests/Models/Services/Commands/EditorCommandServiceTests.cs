using ReiEditor.Models.EditorApp.Commands;
using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Commands;

[Trait("Category", "Unit")]
[Trait("Area", "EditorCommands")]
public sealed class EditorCommandServiceTests
{
    private sealed class Context : IDisposable
    {
        public Scene Scene { get; } = new("History");
        public GameEntity Entity { get; } = new(1, "Before");
        public TestSceneManagementService Scenes { get; } = new();
        public TestEntityManagementService Entities { get; } = new();
        public EntityRenameCommand Rename { get; }
        public EditorCommandService Commands { get; }

        public Context()
        {
            Scene.AddEntity(Entity);
            Scenes.Scene.Value = Scene;
            ApplyNames();
            Rename = new(Entities, new TestLogger<EntityRenameCommand>(), Scenes);
            Commands = new();
        }

        public void ApplyNames() => Entities.OnRename = (entity, name) => entity.SetName(string.IsNullOrEmpty(name) ? $"Entity {entity.Id}" : name);
        public Task<EntityRenameCommandResult> Execute(string name) => Commands.ExecuteAsync((IEntityRenameCommand)Rename, new EntityRenameCommandTarget(Entity, name));
        public void Dispose() => Commands.Dispose();
    }

    private sealed class DeferredRename(EntityRenameCommand inner, Task ready) : IUndoableEditorCommand<EntityRenameCommandTarget, EntityRenameCommandResult>
    {
        public async Task<EntityRenameCommandResult> ExecuteAsync(EntityRenameCommandTarget args)
        {
            await ready;
            return await inner.ExecuteAsync(args);
        }

        public Func<IEditorHistoryEntry, IEditorHistoryEntry>? WrapEntry { get; init; }

        public IEditorHistoryEntry? CreateHistoryEntry(EntityRenameCommandTarget args, EntityRenameCommandResult result)
        {
            var entry = inner.CreateHistoryEntry(args, result);
            return entry == null ? null : WrapEntry?.Invoke(entry) ?? entry;
        }
    }

    private sealed class DeferredEntry(IEditorHistoryEntry inner) : IEditorHistoryEntry
    {
        public Task Ready { get; set; } = Task.CompletedTask;
        public bool CanUndo => inner.CanUndo;
        public bool CanRedo => inner.CanRedo;
        public async Task UndoAsync() { await Ready; await inner.UndoAsync(); }
        public async Task RedoAsync() { await Ready; await inner.RedoAsync(); }
    }

    private sealed class PlainCommand<TArgs, TResult>(Func<TArgs, Task<TResult>> execute) : IEditorCommand<TArgs, TResult>
    {
        public Task<TResult> ExecuteAsync(TArgs args) => execute(args);
    }

    private sealed class ValueCommand : IUndoableEditorCommand<int, int>
    {
        private sealed class Entry(ValueCommand command, int before, int after) : IEditorHistoryEntry
        {
            public bool CanUndo => command.Value == after;
            public bool CanRedo => command.Value == before;
            public Task UndoAsync()
            {
                if (!CanUndo) throw new InvalidOperationException();
                return command.ExecuteAsync(before);
            }
            public Task RedoAsync()
            {
                if (!CanRedo) throw new InvalidOperationException();
                return command.ExecuteAsync(after);
            }
        }

        public int Value { get; private set; }
        public Task<int> ExecuteAsync(int value)
        {
            var before = Value;
            Value = value;
            return Task.FromResult(before);
        }
        public IEditorHistoryEntry? CreateHistoryEntry(int value, int before) => before == value ? null : new Entry(this, before, value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PendingReplayIsAwaitedAndRejectsConcurrentOperations(bool undo)
    {
        using var c = new Context();
        DeferredEntry? entry = null;
        var command = new DeferredRename(c.Rename, Task.CompletedTask)
        {
            WrapEntry = inner => entry = new DeferredEntry(inner)
        };
        await c.Commands.ExecuteAsync(command, new EntityRenameCommandTarget(c.Entity, "After"));
        if (!undo) await c.Commands.UndoAsync();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        entry!.Ready = ready.Task;

        var operation = undo ? c.Commands.UndoAsync() : c.Commands.RedoAsync();
        Assert.False(operation.IsCompleted);
        Assert.True(c.Commands.IsBusy);
        Assert.False(c.Commands.CanUndo);
        Assert.False(c.Commands.CanRedo);
        Assert.Equal(undo ? "After" : "Before", c.Entity.Name);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Execute("Concurrent"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Commands.UndoAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Commands.RedoAsync());
        ready.SetResult();
        await operation;

        Assert.False(c.Commands.IsBusy);
        Assert.Equal(undo ? "Before" : "After", c.Entity.Name);
        Assert.Equal(!undo, c.Commands.CanUndo);
        Assert.Equal(undo, c.Commands.CanRedo);
    }

    [Fact]
    public async Task RenameUndoRedoUsesStoredNamesWithoutRecordingReplay()
    {
        using var c = new Context();
        var result = await c.Execute("After");
        Assert.True(result.Changed);
        Assert.True(c.Commands.CanUndo);
        Assert.False(c.Commands.CanRedo);

        await c.Commands.UndoAsync();
        Assert.Equal("Before", c.Entity.Name);
        Assert.False(c.Commands.CanUndo);
        Assert.True(c.Commands.CanRedo);
        await c.Commands.RedoAsync();
        Assert.Equal("After", c.Entity.Name);
        Assert.True(c.Commands.CanUndo);
        Assert.False(c.Commands.CanRedo);
        await c.Commands.UndoAsync();
        await c.Commands.UndoAsync();
        Assert.Equal("Before", c.Entity.Name);
        Assert.Equal(new[] { "After", "Before", "After", "Before" }, c.Entities.RenameCalls.Select(call => call.Name));
    }

    [Fact]
    public async Task MultipleRenamesTraverseInStackOrder()
    {
        using var c = new Context();
        await c.Execute("One");
        await c.Execute("Two");
        await c.Commands.UndoAsync();
        Assert.Equal("One", c.Entity.Name);
        await c.Commands.UndoAsync();
        Assert.Equal("Before", c.Entity.Name);
        await c.Commands.RedoAsync();
        Assert.Equal("One", c.Entity.Name);
        await c.Commands.RedoAsync();
        Assert.Equal("Two", c.Entity.Name);
    }

    [Fact]
    public async Task NoopPreservesRedoAndNewChangeClearsIt()
    {
        using var c = new Context();
        await c.Execute("After");
        await c.Commands.UndoAsync();
        Assert.False((await c.Execute("Before")).Changed);
        Assert.True(c.Commands.CanRedo);
        Assert.False(c.Commands.CanUndo);
        await c.Execute("Branch");
        Assert.False(c.Commands.CanRedo);
        await c.Commands.RedoAsync();
        Assert.Equal("Branch", c.Entity.Name);
        await c.Commands.UndoAsync();
        Assert.Equal("Before", c.Entity.Name);
    }

    [Fact]
    public async Task NormalizedNameIsStoredInsteadOfRawInput()
    {
        using var c = new Context();
        var result = await c.Execute("");
        Assert.Equal("Entity 1", result.AfterName);
        await c.Commands.UndoAsync();
        Assert.Equal("Before", c.Entity.Name);
        await c.Commands.RedoAsync();
        Assert.Equal("Entity 1", c.Entity.Name);
        Assert.Equal("Entity 1", c.Entities.RenameCalls.Last().Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedExecutePreservesRedoAndReleasesBusy(bool throws)
    {
        using var c = new Context();
        await c.Execute("After");
        await c.Commands.UndoAsync();
        c.Entities.OnRename = (_, _) => { if (throws) throw new InvalidOperationException("rename failed"); };
        if (throws) await Assert.ThrowsAsync<InvalidOperationException>(() => c.Execute("Unapplied"));
        else Assert.False((await c.Execute("Unapplied")).Changed);
        Assert.Equal("Before", c.Entity.Name);
        Assert.False(c.Commands.IsBusy);
        Assert.True(c.Commands.CanRedo);
        Assert.False(c.Commands.CanUndo);
        c.ApplyNames();
        await c.Commands.RedoAsync();
        Assert.Equal("After", c.Entity.Name);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task FailedReplayLeavesEntryAvailableForRetry(bool undo, bool throws)
    {
        using var c = new Context();
        await c.Execute("After");
        if (!undo) await c.Commands.UndoAsync();
        c.Entities.OnRename = (_, _) => { if (throws) throw new InvalidOperationException("rename failed"); };

        await Assert.ThrowsAsync<InvalidOperationException>(() => undo ? c.Commands.UndoAsync() : c.Commands.RedoAsync());

        Assert.Equal(undo ? "After" : "Before", c.Entity.Name);
        Assert.Equal(undo, c.Commands.CanUndo);
        Assert.Equal(!undo, c.Commands.CanRedo);
        Assert.False(c.Commands.IsBusy);
        c.ApplyNames();
        if (undo) await c.Commands.UndoAsync();
        else await c.Commands.RedoAsync();
        Assert.Equal(undo ? "Before" : "After", c.Entity.Name);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExternalNameChangeRejectsReplayWithoutCallingRename(bool undo)
    {
        using var c = new Context();
        await c.Execute("After");
        if (!undo) await c.Commands.UndoAsync();
        var calls = c.Entities.RenameCalls.Count;
        c.Entity.SetName("External");
        await Assert.ThrowsAsync<InvalidOperationException>(() => undo ? c.Commands.UndoAsync() : c.Commands.RedoAsync());
        Assert.Equal("External", c.Entity.Name);
        Assert.Equal(calls, c.Entities.RenameCalls.Count);
        Assert.False(c.Commands.CanUndo);
        Assert.False(c.Commands.CanRedo);
        c.Entity.SetName(undo ? "After" : "Before");
        Assert.Equal(undo, c.Commands.CanUndo);
        Assert.Equal(!undo, c.Commands.CanRedo);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletedOrReplacedEntityCannotReceiveOldRename(bool replace)
    {
        using var c = new Context();
        await c.Execute("After");
        c.Scene.DeleteEntity(c.Entity);
        if (replace) c.Scene.AddEntity(new GameEntity(1, "After"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Commands.UndoAsync());
        Assert.Single(c.Entities.RenameCalls);
        Assert.False(c.Commands.CanUndo);
        if (replace) Assert.Equal("After", c.Scene.GetById(1)!.Name);
    }

    [Fact]
    public async Task SceneChangePreservesHistoryAndRenameControlsAvailability()
    {
        using var c = new Context();
        await c.Execute("One");
        await c.Execute("Two");
        await c.Commands.UndoAsync();
        Assert.True(c.Commands.CanUndo);
        Assert.True(c.Commands.CanRedo);

        c.Scenes.Scene.Value = new Scene("Other");
        Assert.False(c.Commands.CanUndo);
        Assert.False(c.Commands.CanRedo);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Commands.UndoAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Commands.RedoAsync());

        c.Scenes.Scene.Value = c.Scene;
        Assert.True(c.Commands.CanUndo);
        Assert.True(c.Commands.CanRedo);
        await c.Commands.RedoAsync();
        Assert.Equal("Two", c.Entity.Name);
        await c.Commands.UndoAsync();
        await c.Commands.UndoAsync();
        Assert.Equal("Before", c.Entity.Name);
    }

    [Fact]
    public async Task PendingCommandRejectsConcurrentWorkWithoutSceneVersionTracking()
    {
        using var c = new Context();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new DeferredRename(c.Rename, ready.Task);
        var pending = c.Commands.ExecuteAsync(command, new EntityRenameCommandTarget(c.Entity, "Late"));
        Assert.True(c.Commands.IsBusy);
        Assert.False(c.Commands.CanUndo);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Execute("Concurrent"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Commands.UndoAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Commands.RedoAsync());
        c.Scenes.Scene.Value = new Scene("Other");
        c.Scenes.Scene.Value = c.Scene;
        ready.SetResult();
        Assert.True((await pending).Changed);
        Assert.False(c.Commands.IsBusy);
        Assert.True(c.Commands.CanUndo);
        Assert.False(c.Commands.CanRedo);
        Assert.Single(c.Entities.RenameCalls);
        await c.Commands.UndoAsync();
        Assert.Equal("Before", c.Entity.Name);
    }

    [Fact]
    public async Task EmptyHistoryAndPlainCommandsNeedNoScene()
    {
        using var commands = new EditorCommandService();
        await commands.UndoAsync();
        await commands.RedoAsync();
        var command = new PlainCommand<int, int>(value => Task.FromResult(value + 1));

        Assert.Equal(6, await commands.ExecuteAsync(command, 5));
        Assert.False(commands.CanUndo);
        Assert.False(commands.CanRedo);
        Assert.False(commands.IsBusy);
    }

    [Fact]
    public async Task PlainCommandIsAwaitedAndFailureReleasesBusy()
    {
        using var commands = new EditorCommandService();
        var ready = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new PlainCommand<int, int>(_ => ready.Task);
        var pending = commands.ExecuteAsync(command, 5);
        Assert.False(pending.IsCompleted);
        Assert.True(commands.IsBusy);
        ready.SetException(new InvalidOperationException("command failed"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
        Assert.False(commands.IsBusy);
        Assert.False(commands.CanUndo);
        Assert.False(commands.CanRedo);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RenameItselfRejectsMissingOrDifferentScene(bool missing)
    {
        using var c = new Context();
        c.Scenes.Scene.Value = missing ? null : new Scene("Other");
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Execute("Unapplied"));
        Assert.Equal("Before", c.Entity.Name);
        Assert.Empty(c.Entities.RenameCalls);
        Assert.False(c.Commands.IsBusy);
    }

    [Fact]
    public async Task NonSceneUndoSurvivesSceneChangesInMixedHistory()
    {
        using var c = new Context();
        var config = new ValueCommand();
        await c.Execute("After");
        await c.Commands.ExecuteAsync(config, 42);
        c.Scenes.Scene.Value = null;

        Assert.True(c.Commands.CanUndo);
        await c.Commands.UndoAsync();
        Assert.Equal(0, config.Value);
        Assert.False(c.Commands.CanUndo);
        Assert.True(c.Commands.CanRedo);
        await c.Commands.RedoAsync();
        Assert.Equal(42, config.Value);
        await c.Commands.UndoAsync();

        c.Scenes.Scene.Value = c.Scene;
        Assert.True(c.Commands.CanUndo);
        await c.Commands.UndoAsync();
        Assert.Equal("Before", c.Entity.Name);
    }

    [Fact]
    public async Task DisposedServiceRejectsFurtherOperations()
    {
        using var c = new Context();
        await c.Execute("After");
        c.Commands.Dispose();
        Assert.False(c.Commands.CanUndo);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => c.Execute("Unapplied"));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => c.Commands.UndoAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => c.Commands.RedoAsync());
        Assert.Equal("After", c.Entity.Name);
    }
}
