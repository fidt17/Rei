using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Engine.Playmode;

public class PlaymodeService : IPlaymodeService, IDisposable
{
    public Utils.Common.IObservable<bool> IsPlaymodeActive => _isPlaymodeActive;

    private readonly Observable<bool> _isPlaymodeActive = new(false);
    private readonly ILogger<PlaymodeService> _logger;
    private readonly IClientDllManager _clientDllManager;
    private readonly IPlaymodeRunner _playmodeRunner;

    public PlaymodeService(ILogger<PlaymodeService> logger, IClientDllManager clientDllManager, IPlaymodeRunner playmodeRunner)
    {
        _logger = logger;
        _clientDllManager = clientDllManager;
        _playmodeRunner = playmodeRunner;
        
        _playmodeRunner.PlaymodeFailedEvent += HandlePlaymodeFailedEvent;
        _playmodeRunner.PlaymodeExitedEvent += HandlePlaymodeExitedEvent;
    }

    public void Dispose()
    {
        if (_isPlaymodeActive)
        {
            StopPlaymode();
        }
        
        _playmodeRunner.PlaymodeFailedEvent -= HandlePlaymodeFailedEvent;
        _playmodeRunner.PlaymodeExitedEvent -= HandlePlaymodeExitedEvent;
    }

    public void StartPlaymode()
    {
        Task.Run(() =>
        {
            if (_isPlaymodeActive)
            {
                _logger.LogError("Playmode is already active");
                return;
            }

            _logger.Log("Start Playmode");
			
            try
            {
                if (_clientDllManager.DllLoaded.Value)
                {
                    _clientDllManager.UnloadDll();
                }
			
                _clientDllManager.LoadDll();
            }
            catch (Exception e)
            {
                _logger.LogError("Could not load client dll");
                _logger.LogException(e);
                return;
            }

            try
            {
                _playmodeRunner.StartPlaymode();
            }
            catch (Exception e)
            {
                _logger.LogError("Could not start Playmode");
                _logger.LogException(e);
                return;
            }

            _isPlaymodeActive.Value = true;
        });
    }

    public void StopPlaymode()
    {
        if (!_isPlaymodeActive) return;

        _logger.Log("Stop Playmode");

        Task.Run(() =>
        {
            try
            {
                _playmodeRunner.StopPlaymode();
            }
            catch (Exception e)
            {
                _logger.LogError("Could not stop Playmode");
                _logger.LogException(e);
            }
        });
    }

    private void HandlePlaymodeFailedEvent()
    {
        StopPlaymode();
        try
        {
            _isPlaymodeActive.Value = false;
            _clientDllManager.UnloadDll();
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }
    }

    private void HandlePlaymodeExitedEvent()
    {
        Task.Run(() =>
        {
            try
            {
                _clientDllManager.UnloadDll();
                _isPlaymodeActive.Value = false;
            }
            catch (Exception e)
            {
                _logger.LogError("Could not unload client dll");
                _logger.LogException(e);
            }
        });
    }
}