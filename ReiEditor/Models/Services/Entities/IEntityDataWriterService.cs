using System.Collections.Generic;

namespace ReiEditor.Models.Services.Entities;

public interface IEntityDataWriterService
{
    bool SetBehaviourProperty(GameEntity entity, int behaviourId, string propertyName, object? value);
    bool SetBehaviourProperty(GameEntity entity, int behaviourId, string propertyName, IReadOnlyDictionary<string, object?> value);
}
