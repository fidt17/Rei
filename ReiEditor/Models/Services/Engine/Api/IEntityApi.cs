using ReiEditor.Models.Services.Engine.Api.DTO;

namespace ReiEditor.Models.Services.Engine.Api;

public interface IEntityApi
{
    GetEntityDataResponse? GetEntityData(int sceneEntityId);
    bool RenameEntity(int sceneEntityId, string newName);
    bool SetEntityData(SetEntityDataRequest request);
}