using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Api;

namespace ReiEditor.Models.Services.Engine.Playmode;

public class EngineShutdownListener : IEngineShutdownListener
{
    public event Action<int>? EngineShutdownEvent;

    private readonly IEngineApi.IntPtrCallbackDelegate _shutdownCallbackDelegate;
    private readonly IEngineApi _engineApi;

    public EngineShutdownListener(IEngineApi engineApi)
    {
        _engineApi = engineApi;
        _shutdownCallbackDelegate = HandleShutdownEvent;
    }
    
    public void SubscribeToClient()
    {
        _engineApi.AddShutdownCallback(Marshal.GetFunctionPointerForDelegate(_shutdownCallbackDelegate));
    }

    private void HandleShutdownEvent(IntPtr args)
    {
        Task.Run(async () =>
        {
            // Delay for engine to completely stop
            await Task.Delay(100); 
            EngineShutdownEvent?.Invoke(0);
        });
    }
}