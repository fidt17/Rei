using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Entities.Sync;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

internal sealed class TestEntitySyncService : IEntitySyncService
{
    public Action<GameEntity>? OnUpdate { get; set; }
    public void UpdateEntityState(GameEntity entity) => (OnUpdate ?? throw new NotSupportedException())(entity);
}
