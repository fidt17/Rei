using ReiEditor.Models.Services.Engine.Api.DTO;

namespace ReiEditor.Models.Services.Entities.Sync;

public interface IEntityStateApplier
{
    bool IsApplyingEngineState { get; }
    bool Apply(GameEntity entity, GetEntityDataResponse state);
}