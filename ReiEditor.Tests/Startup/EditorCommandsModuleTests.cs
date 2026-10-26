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

[Trait("Category", "Composition")]
[Trait("Area", "EditorCommands")]
public sealed class EditorCommandsModuleTests
{
    private sealed class Context : IDisposable
    {
        public GameEntity Entity { get; } = new(1, "Before");
        public TestEntityManagementService Entities { get; } = new();
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
        builder.RegisterModule<EditorCommandsModule>();
        using var container = builder.Build();
        var commands = container.Resolve<IEditorCommandService>();
        Assert.False(commands.IsBusy);
        Assert.False(commands.CanUndo);
        Assert.False(commands.CanRedo);
    }

    [Fact]
    public void ModuleOwnsEditorCommandRegistrations()
    {
        using var c = new Context();
        foreach (var type in new[]
        {
            typeof(IEditorCommandService), typeof(IEntityRenameCommand),
            typeof(ISelectedEntityDeleteCommand), typeof(ISelectedEntityDuplicateCommand), typeof(ISelectedEntityRenameCommand),
            typeof(IProjectAssetDeleteCommand), typeof(IProjectAssetDuplicateCommand), typeof(IProjectAssetMoveCommand), typeof(IProjectAssetRenameCommand),
            typeof(CreateSceneEntityCommand), typeof(SaveProjectCommand)
        }) Assert.True(c.Container.IsRegistered(type), type.Name);
    }
}
