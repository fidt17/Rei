using ReiEditor.Models.Services.Engine.Api.DTO;

namespace ReiEditor.Models.Services.Engine.Api;

public interface IEntityApi
{
    GetEntityDataResponse? GetData(int sceneEntityId);
    
    void Rename(int sceneEntityId, string newName);
    void SetData(SetEntityDataRequest request);

    void AddBehaviour(int sceneEntityId, int behaviourId);
    void DeleteBehaviour(int sceneEntityId, int behaviourId);
}