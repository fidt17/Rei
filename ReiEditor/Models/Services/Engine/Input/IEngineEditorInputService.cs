using System;
using ReiEditor.Models.Services.Engine.Api.DTO;

namespace ReiEditor.Models.Services.Engine.Input;

public interface IEngineEditorInputService
{
    event Action<EngineEditorInputEvent>? InputReceivedEvent;

    void SubscribeToClient();
}
