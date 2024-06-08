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

    private IntPtr? _enginePtr;
	
    private readonly IEngineApi _engineApi;
    private readonly ILogger<PlaymodeRunner> _logger;
    private readonly IEngineLogger _engineLogger;
    private readonly IPlaymodeWindowController _playmodeWindowController;

    public PlaymodeRunner(IEngineApi engineApi, ILogger<PlaymodeRunner> logger, IEngineLogger engineLogger, IPlaymodeWindowController playmodeWindowController)
    {
        _engineApi = engineApi;
        _logger = logger;
        _engineLogger = engineLogger;
        _playmodeWindowController = playmodeWindowController;
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
        _playmodeWindowController.DestroyWindow();
		
        _enginePtr = null;
    }

    private void SetupPlaymode()
    {
        _engineLogger.SubscribeToClient();
        _playmodeWindowController.SetupWindow();
    }
}