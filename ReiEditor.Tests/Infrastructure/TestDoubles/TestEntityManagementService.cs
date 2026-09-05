using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

/// <summary>Records entity operations and requires explicit responses for calls that create results.</summary>
internal sealed class TestEntityManagementService : IEntityManagementService
{
    public List<(string Name, GameEntity? Parent)> CreateCalls { get; } = new();
    public List<(GameEntity Entity, int BehaviourId)> AddBehaviourCalls { get; } = new();
    public List<GameEntity> DestroyCalls { get; } = new();
    public List<(GameEntity Entity, string Name)> RenameCalls { get; } = new();
    public List<(GameEntity Entity, string? Name, bool IncludeChildren)> InstantiateCalls { get; } = new();
    public Func<string, GameEntity?, Task<GameEntity?>>? OnCreate { get; set; }
    public Action<GameEntity, int>? OnAddBehaviour { get; set; }
    public Action<GameEntity>? OnDestroy { get; set; }
    public Action<GameEntity, string>? OnRename { get; set; }
    public Func<GameEntity, string?, bool, int?>? OnInstantiate { get; set; }

    public Task<GameEntity?> CreateEntity(string name, GameEntity? parent = null)
    {
        CreateCalls.Add((name, parent));
        return (OnCreate ?? throw new NotSupportedException())(name, parent);
    }

    public void AddBehaviour(GameEntity e, int behaviourId)
    {
        AddBehaviourCalls.Add((e, behaviourId));
        (OnAddBehaviour ?? throw new NotSupportedException())(e, behaviourId);
    }

    public void DestroyEntity(GameEntity e)
    {
        DestroyCalls.Add(e);
        (OnDestroy ?? throw new NotSupportedException())(e);
    }

    public void RenameEntity(GameEntity e, string name)
    {
        RenameCalls.Add((e, name));
        (OnRename ?? throw new NotSupportedException())(e, name);
    }

    public int? InstantiateEntity(GameEntity sourceEntity, string? requestedName = null, bool includeChildren = true)
    {
        InstantiateCalls.Add((sourceEntity, requestedName, includeChildren));
        return (OnInstantiate ?? throw new NotSupportedException())(sourceEntity, requestedName, includeChildren);
    }

    public void SetParent(GameEntity e, GameEntity? parent, int idx) => throw new NotSupportedException();
    public void DeleteBehaviour(GameEntity e, int behaviourId) => throw new NotSupportedException();
}
