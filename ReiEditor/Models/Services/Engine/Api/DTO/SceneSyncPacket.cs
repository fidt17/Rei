using System;

namespace ReiEditor.Models.Services.Engine.Api.DTO;

public sealed class SceneSyncPacket
{
    public string? SceneAssetId { get; set; }
    public string? SceneName { get; set; }
    public bool IsSnapshot { get; set; }
    public SceneEntityState[] Entities { get; set; } = Array.Empty<SceneEntityState>();
    public int[] RemovedEntityIds { get; set; } = Array.Empty<int>();
    public int[] HistoryRemovedEntityIds { get; set; } = Array.Empty<int>();
    public int[] DiscardedHistoryEntityIds { get; set; } = Array.Empty<int>();
    public int[] SelectedEntityIds { get; set; } = Array.Empty<int>();
}

public sealed record SceneEntityState(int Id, string Name, int ParentId, int Order);
