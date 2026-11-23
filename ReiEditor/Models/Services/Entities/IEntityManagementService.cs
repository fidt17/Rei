using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Entities;

public interface IEntityManagementService
{
    Task<GameEntity?> CreateEntity(string name, GameEntity? parent = null, bool recordHistory = false);

    void RenameEntity(GameEntity e, string name);
    void ReorderEntities(IReadOnlyList<GameEntity> entities, int insertionIndex);
    bool CanReparentEntities(IReadOnlyList<GameEntity> entities, GameEntity? parent);
    void ReparentEntities(IReadOnlyList<GameEntity> entities, GameEntity? parent, int insertionIndex);
    void SetParent(GameEntity e, GameEntity? parent, int idx, bool recordHierarchyChange = false);
    
    void AddBehaviour(GameEntity e, int behaviourId);
    void DeleteBehaviour(GameEntity e, int behaviourId);

    int? InstantiateEntity(GameEntity sourceEntity, string? requestedName = null, bool includeChildren = true, bool recordHistory = false);
	
    IReadOnlyList<int> InstantiateEntities(IReadOnlyList<GameEntity> sources);
    void DestroyEntity(GameEntity e, bool recordHistory = false);
    void DestroyEntities(IReadOnlyList<GameEntity> entities);
}
