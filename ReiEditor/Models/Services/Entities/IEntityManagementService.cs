namespace ReiEditor.Models.Services.Entities;

public interface IEntityManagementService
{
    GameEntity? CreateEntity(string name);

    void RenameEntity(GameEntity e, string name);
    void SetParent(GameEntity e, GameEntity? parent, int idx);
    
    void AddBehaviour(GameEntity e, int behaviourId);
    void DeleteBehaviour(GameEntity e, int behaviourId);
	
    void DestroyEntity(GameEntity e);
}