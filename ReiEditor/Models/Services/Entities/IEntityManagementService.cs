using System;

namespace ReiEditor.Models.Services.Entities;

public interface IEntityManagementService
{
	event Action<GameEntity> EntityCreatedEvent;
	event Action<GameEntity> EntityDeletedEvent;
	
	GameEntity? CreateEntity(string name);

	void RenameEntity(GameEntity e, string name);
	
	void DeleteEntity(GameEntity e);
}