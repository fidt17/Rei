using ReiEditor.Models.Services.Engine.Api.DTO;

namespace ReiEditor.Models.Services.Engine.Api;

public interface IEntityApi
{
    GetSceneEntitiesResponse? GetSceneEntities();
    
    GetEntityDataResponse? GetEntityData(int sceneEntityId);

    void CreateNewEntity(string name);
    void DestroyEntity(int sceneEntityId);
    
    void Rename(int sceneEntityId, string newName);
    void SetData(SetEntityDataRequest request);

    void AddBehaviour(int sceneEntityId, int behaviourId);
    void DeleteBehaviour(int sceneEntityId, int behaviourId);
}