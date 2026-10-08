using System;
using ReiEditor.Models.Services.Engine.Api.DTO;

namespace ReiEditor.Models.Services.Engine.SceneSync;

public interface IEngineSceneSyncEventSource
{
    event Action<SceneSyncPacket>? Received;
    event Action? ConnectionReset;
    bool IsConnected { get; }
    void Connect();
    void Disconnect();
    void RequestSnapshot();
}
