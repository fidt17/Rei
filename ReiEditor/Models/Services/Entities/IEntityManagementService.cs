using System;

namespace ReiEditor.Models.Services.Entities;

public interface IEntityManagementService
{
    event Action<GameEntity> EntityCreatedEvent;
    event Action<GameEntity> EntityMovedEvent;
    event Action<GameEntity> EntityDeletedEvent;
	
    GameEntity? CreateEntity(string name);

    void RenameEntity(GameEntity e, string name);
    void SetParent(GameEntity e, GameEntity? parent, int idx);
    
    void AddBehaviour(GameEntity e, int behaviourId);
    void DeleteBehaviour(GameEntity e, int behaviourId);
	
    void DeleteEntity(GameEntity e);

    void UpdateEntityStateFromEngine(GameEntity e);
}