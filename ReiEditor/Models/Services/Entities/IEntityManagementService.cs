using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Entities;

public interface IEntityManagementService
{
    Task<GameEntity?> CreateEntity(string name, GameEntity? parent = null);

    void RenameEntity(GameEntity e, string name);
    void SetParent(GameEntity e, GameEntity? parent, int idx);
    
    void AddBehaviour(GameEntity e, int behaviourId);
    void DeleteBehaviour(GameEntity e, int behaviourId);

    int? InstantiateEntity(GameEntity sourceEntity, string? requestedName = null, bool includeChildren = true);
	
    void DestroyEntity(GameEntity e);
}
