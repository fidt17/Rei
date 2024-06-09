using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Logging.Engine;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Windows.Playmode;

namespace ReiEditor.Models.Services.Engine.Playmode;

public class PlaymodeRunner : IPlaymodeRunner
{
    public event Action? PlaymodeFailedEvent;
    public event Action? PlaymodeExitedEvent;

    private IntPtr? _enginePtr;
	
    private readonly IEngineApi _engineApi;
    private readonly ILogger<PlaymodeRunner> _logger;
    private readonly IEngineLogger _engineLogger;
    private readonly IPlaymodeWindowController _playmodeWindowController;
    private readonly IEngineShutdownListener _shutdownListener;

    public PlaymodeRunner(
        IEngineApi engineApi,
        ILogger<PlaymodeRunner> logger,
        IEngineLogger engineLogger,
        IPlaymodeWindowController playmodeWindowController,
        IEngineShutdownListener shutdownListener)
    {
        _engineApi = engineApi;
        _logger = logger;
        _engineLogger = engineLogger;
        _playmodeWindowController = playmodeWindowController;
        _shutdownListener = shutdownListener;
        
        _shutdownListener.EngineShutdownEvent += HandleEngineShutdownEvent;
    }
    
    public void StartPlaymode()
    {
        if (_enginePtr != null) throw new Exception("EnginePtr already exists");

        Task.Run(() =>
        {
            try
            {
                _enginePtr = _engineApi.CreateEngine();

                SetupPlaymode();
                _engineApi.Start(_enginePtr.Value);
            }
            catch (Exception e)
            {
                _logger.LogException(e);
                PlaymodeFailedEvent?.Invoke();
            }
        });
    }

    public void StopPlaymode()
    {
        if (_enginePtr == null) throw new Exception("EnginePtr is missing");

        _engineApi.Shutdown(_enginePtr.Value, 1);
    }

    private void SetupPlaymode()
    {
        _engineLogger.SubscribeToClient();
        _shutdownListener.SubscribeToClient();
        _playmodeWindowController.SetupWindow();
    }

    private void HandleEngineShutdownEvent(int obj)
    {
        _playmodeWindowController.DestroyWindow();
        _enginePtr = null;
        
        PlaymodeExitedEvent?.Invoke();
    }
}