using System.Collections.Generic;

namespace ReiEditor.Models.Services.Entities.Sync;

public interface IEngineSelectionSyncService
{
    void ApplySelection(IEnumerable<GameEntity> entities, IReadOnlySet<int> selectedEntityIds);
}
