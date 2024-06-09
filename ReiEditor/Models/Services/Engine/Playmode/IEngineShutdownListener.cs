using System;

namespace ReiEditor.Models.Services.Engine.Playmode;

public interface IEngineShutdownListener
{
    event Action<int>? EngineShutdownEvent;

    void SubscribeToClient();
}