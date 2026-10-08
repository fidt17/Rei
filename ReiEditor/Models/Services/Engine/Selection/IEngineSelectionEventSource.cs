using System;

namespace ReiEditor.Models.Services.Engine.Selection;

public interface IEngineSelectionEventSource
{
    event Action<int[]>? SelectionReceived;
    bool IsEventSyncEnabled { get; }
    void Connect();
    void Disconnect();
}
