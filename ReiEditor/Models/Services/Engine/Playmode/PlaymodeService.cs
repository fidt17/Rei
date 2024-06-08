using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Factory;

namespace ReiEditor.Models.Services.Engine.Playmode;

public class PlaymodeService : IPlaymodeService, IDisposable
{
    public Utils.Common.IObservable<bool> IsPlaymodeActive => _isPlaymodeActive;

    private IPlaymodeRunner? _activePlaymodeRunner;

    private readonly Observable<bool> _isPlaymodeActive = new(false);
    private readonly ILogger<PlaymodeService> _logger;
    private readonly IClientDllManager _clientDllManager;
    private readonly IFactory<IPlaymodeRunner> _playmodeRunnerFactory;

    public PlaymodeService(ILogger<PlaymodeService> logger, IClientDllManager clientDllManager, IFactory<IPlaymodeRunner> playmodeRunnerFactory)
    {
        _logger = logger;
        _clientDllManager = clientDllManager;
        _playmodeRunnerFactory = playmodeRunnerFactory;
    }
	
    public void Dispose()
    {
        if (_isPlaymodeActive)
        {
            StopPlaymode();
        }
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
            _activePlaymodeRunner = _playmodeRunnerFactory.CreateInstance();
            _activePlaymodeRunner.PlaymodeFailedEvent += StopPlaymode;
			
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
                _activePlaymodeRunner.StartPlaymode();
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

        Task.Run(async () =>
        {
            try
            {
                _activePlaymodeRunner?.StopPlaymode();
                await Task.Delay(1000);
            }
            catch (Exception e)
            {
                _logger.LogError("Could not stop Playmode");
                _logger.LogException(e);
            }
			
            try
            {
                _clientDllManager.UnloadDll();
            }
            catch (Exception e)
            {
                _logger.LogError("Could not unload client dll");
                _logger.LogException(e);
            }

            _isPlaymodeActive.Value = false;
        });
    }
}