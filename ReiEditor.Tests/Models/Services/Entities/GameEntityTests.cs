using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Tests.Models.Services.Entities;

/// <summary>
/// Verifies game entity identity, naming, transform, and behaviour lifecycle contracts.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Entities")]
public sealed class GameEntityTests
{
    /// <summary>
    /// Constructor preserves identity and name and creates a root transform.
    /// </summary>
    [Fact]
    public void TestConstructorInitializesIdentityNameAndTransform()
    {
        var entity = new GameEntity(42, "Player");

        Assert.Equal(42, entity.Id);
        Assert.Equal("Player", entity.Name);
        Assert.NotNull(entity.Transform);
        Assert.False(entity.Transform.HasParent());
        Assert.Equal(0, entity.Transform.Order);
        Assert.Empty(entity.Behaviours);
    }

    /// <summary>
    /// Changing name updates state before publishing entity and new name.
    /// </summary>
    [Fact]
    public void TestSetNameChangesNameAndRaisesPostMutationEvent()
    {
        var entity = new GameEntity(1, "Old");
        (GameEntity Entity, string Name)? received = null;
        entity.NameChangedEvent += (changedEntity, name) =>
        {
            Assert.Equal("New", changedEntity.Name);
            received = (changedEntity, name);
        };

        entity.SetName("New");

        Assert.Equal("New", entity.Name);
        Assert.True(received.HasValue);
        Assert.Same(entity, received.Value.Entity);
        Assert.Equal("New", received.Value.Name);
    }

    /// <summary>
    /// Assigning equal name does not publish a redundant change.
    /// </summary>
    [Fact]
    public void TestSetNameWithEqualValueDoesNotRaiseEvent()
    {
        var entity = new GameEntity(1, "Same");
        var eventCount = 0;
        entity.NameChangedEvent += (_, _) => eventCount++;

        entity.SetName("Same");

        Assert.Equal(0, eventCount);
    }

    /// <summary>
    /// Adding behaviour makes it queryable and publishes event after insertion.
    /// </summary>
    [Fact]
    public void TestAddBehaviourRegistersBehaviourAndRaisesPostMutationEvent()
    {
        var entity = new GameEntity(1, "Entity");
        var behaviour = new BehaviourComponent(10);
        BehaviourComponent? received = null;
        entity.BehaviourAddedEvent += (changedEntity, added) =>
        {
            Assert.True(changedEntity.HasBehaviour(added));
            received = added;
        };

        entity.AddBehaviour(behaviour);

        Assert.Same(behaviour, received);
        Assert.True(entity.HasComponent(10));
        Assert.True(entity.HasBehaviour(behaviour));
        Assert.Same(behaviour, entity.GetBehaviour(10));
    }

    /// <summary>
    /// Adding same behaviour object twice throws without duplicating collection or event.
    /// </summary>
    [Fact]
    public void TestAddSameBehaviourTwiceThrowsWithoutMutation()
    {
        var entity = new GameEntity(1, "Entity");
        var behaviour = new BehaviourComponent(10);
        var eventCount = 0;
        entity.BehaviourAddedEvent += (_, _) => eventCount++;
        entity.AddBehaviour(behaviour);

        Assert.Throws<Exception>(() => entity.AddBehaviour(behaviour));

        Assert.Same(behaviour, Assert.Single(entity.Behaviours));
        Assert.Equal(1, eventCount);
    }

    /// <summary>
    /// Distinct behaviour objects may share an ID while object membership remains independent.
    /// </summary>
    [Fact]
    public void TestAddDistinctBehavioursWithSameIdPreservesBothObjects()
    {
        var entity = new GameEntity(1, "Entity");
        var first = new BehaviourComponent(10);
        var second = new BehaviourComponent(10);

        entity.AddBehaviour(first);
        entity.AddBehaviour(second);

        Assert.Equal(new[] { first, second }, entity.Behaviours);
        Assert.Same(first, entity.GetBehaviour(10));
    }

    /// <summary>
    /// Deleting behaviour removes it before publishing entity and deleted object.
    /// </summary>
    [Fact]
    public void TestDeleteBehaviourRemovesBehaviourAndRaisesPostMutationEvent()
    {
        var entity = new GameEntity(1, "Entity");
        var behaviour = new BehaviourComponent(10);
        entity.AddBehaviour(behaviour);
        BehaviourComponent? received = null;
        entity.BehaviourDeletedEvent += (changedEntity, deleted) =>
        {
            Assert.False(changedEntity.HasBehaviour(deleted));
            received = deleted;
        };

        entity.DeleteBehaviour(behaviour);

        Assert.Same(behaviour, received);
        Assert.Empty(entity.Behaviours);
        Assert.Null(entity.GetBehaviour(10));
    }

    /// <summary>
    /// Deleting absent behaviour throws without publishing deletion.
    /// </summary>
    [Fact]
    public void TestDeleteAbsentBehaviourThrowsWithoutEvent()
    {
        var entity = new GameEntity(1, "Entity");
        var eventCount = 0;
        entity.BehaviourDeletedEvent += (_, _) => eventCount++;

        Assert.Throws<Exception>(() => entity.DeleteBehaviour(new BehaviourComponent(10)));

        Assert.Equal(0, eventCount);
        Assert.Empty(entity.Behaviours);
    }

    /// <summary>
    /// Entity equality and display text use stable ID and current name.
    /// </summary>
    [Fact]
    public void TestEqualityAndToStringUseIdentityAndCurrentName()
    {
        var entity = new GameEntity(7, "Old");
        entity.SetName("New");

        Assert.True(entity.Equals(new GameEntity(7, "Other")));
        Assert.False(entity.Equals(new GameEntity(8, "New")));
        Assert.Equal("E(New:7)", entity.ToString());
    }
}
