using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.EditorApp.Scene.Commands.Entities;

/// <summary>
/// Verifies selected entity command dispatch, results, ordering, and dependency failures.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "SceneCommands")]
public sealed class SelectedEntityCommandTests
{
    /// <summary>
    /// Delete command destroys every target entity in target order.
    /// </summary>
    [Fact]
    public void TestDeleteCommandDestroysAllTargetEntitiesInOrder()
    {
        var management = new TestEntityManagementService { OnDestroy = _ => { } };
        var command = new SelectedEntityDeleteCommand(management, new TestLogger<SelectedEntityDeleteCommand>());
        var first = new GameEntity(1, "First");
        var second = new GameEntity(2, "Second");

        var result = command.Execute(new SelectedEntityCommandTarget(first, new[] { second, first }));

        Assert.Equal(new[] { second, first }, management.DestroyCalls);
        Assert.Null(result.SelectedEntityIds);
        Assert.Null(result.RenameEntityId);
    }

    /// <summary>
    /// Delete command lets entity management exceptions escape and stops later dispatch.
    /// </summary>
    [Fact]
    public void TestDeleteCommandPropagatesDependencyException()
    {
        var expected = new InvalidOperationException("destroy failed");
        var management = new TestEntityManagementService { OnDestroy = _ => throw expected };
        var command = new SelectedEntityDeleteCommand(management, new TestLogger<SelectedEntityDeleteCommand>());
        var first = new GameEntity(1, "First");
        var second = new GameEntity(2, "Second");

        var actual = Assert.Throws<InvalidOperationException>(() =>
            command.Execute(new SelectedEntityCommandTarget(first, new[] { first, second })));

        Assert.Same(expected, actual);
        Assert.Equal(new[] { first }, management.DestroyCalls);
    }

    /// <summary>
    /// Duplicate command returns only successful duplicate IDs while preserving target order.
    /// </summary>
    [Fact]
    public void TestDuplicateCommandReturnsSuccessfulIdsInOrder()
    {
        var results = new Queue<int?>(new int?[] { 21, null, 23 });
        var management = new TestEntityManagementService { OnInstantiate = (_, _, _) => results.Dequeue() };
        var command = new SelectedEntityDuplicateCommand(management, new TestLogger<SelectedEntityDuplicateCommand>());
        var entities = new[]
        {
            new GameEntity(1, "First"),
            new GameEntity(2, "Second"),
            new GameEntity(3, "Third"),
        };

        var result = command.Execute(new SelectedEntityCommandTarget(entities[0], entities));

        Assert.Equal(new[] { 21, 23 }, result.SelectedEntityIds);
        Assert.Equal(entities, management.InstantiateCalls.Select(call => call.Entity));
        Assert.All(management.InstantiateCalls, call =>
        {
            Assert.Null(call.Name);
            Assert.True(call.IncludeChildren);
        });
    }

    /// <summary>
    /// Duplicate command lets instantiate exceptions escape and stops later dispatch.
    /// </summary>
    [Fact]
    public void TestDuplicateCommandPropagatesDependencyException()
    {
        var expected = new InvalidOperationException("instantiate failed");
        var management = new TestEntityManagementService { OnInstantiate = (_, _, _) => throw expected };
        var command = new SelectedEntityDuplicateCommand(management, new TestLogger<SelectedEntityDuplicateCommand>());
        var entity = new GameEntity(4, "Entity");

        var actual = Assert.Throws<InvalidOperationException>(() =>
            command.Execute(new SelectedEntityCommandTarget(entity, new[] { entity })));

        Assert.Same(expected, actual);
    }

    /// <summary>
    /// Selected rename command reports primary entity ID independent of other selected entities.
    /// </summary>
    [Fact]
    public void TestSelectedRenameCommandReturnsPrimaryEntityId()
    {
        var command = new SelectedEntityRenameCommand(new TestLogger<SelectedEntityRenameCommand>());
        var primary = new GameEntity(10, "Primary");
        var other = new GameEntity(11, "Other");

        var result = command.Execute(new SelectedEntityCommandTarget(primary, new[] { other }));

        Assert.Equal(10, result.RenameEntityId);
        Assert.Null(result.SelectedEntityIds);
    }

    /// <summary>
    /// Entity rename command forwards entity and unmodified requested name to management service.
    /// </summary>
    [Fact]
    public void TestEntityRenameCommandForwardsTarget()
    {
        var management = new TestEntityManagementService { OnRename = (_, _) => { } };
        var command = new EntityRenameCommand(management, new TestLogger<EntityRenameCommand>());
        var entity = new GameEntity(14, "Before");

        command.Execute(new EntityRenameCommandTarget(entity, "  After  "));

        var call = Assert.Single(management.RenameCalls);
        Assert.Same(entity, call.Entity);
        Assert.Equal("  After  ", call.Name);
    }

    /// <summary>
    /// Entity rename command does not mask management service exceptions.
    /// </summary>
    [Fact]
    public void TestEntityRenameCommandPropagatesDependencyException()
    {
        var expected = new InvalidOperationException("rename failed");
        var management = new TestEntityManagementService { OnRename = (_, _) => throw expected };
        var command = new EntityRenameCommand(management, new TestLogger<EntityRenameCommand>());
        var entity = new GameEntity(15, "Before");

        var actual = Assert.Throws<InvalidOperationException>(() =>
            command.Execute(new EntityRenameCommandTarget(entity, "After")));

        Assert.Same(expected, actual);
    }
}
