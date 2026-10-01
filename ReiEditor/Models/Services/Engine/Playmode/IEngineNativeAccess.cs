using System;
using ReiEditor.Models.Services.Engine.Api;

namespace ReiEditor.Models.Services.Engine.Playmode;

public interface IEngineNativeAccess
{
    // Keeps a running native engine and its project DLL alive for this bounded call.
    bool TryInvoke(Action<IEngineApi> action);
}
