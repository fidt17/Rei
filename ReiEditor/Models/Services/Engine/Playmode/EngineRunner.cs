using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Logging.Engine;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Windows.Playmode;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Engine.Playmode;

public class EngineRunner : IEngineRunner, IAsyncDisposable
{
    public event Action? EngineStartedEvent;
    
    public Utils.Common.IObservable<bool> IsActive => _isActive;
    public Utils.Common.IObservable<bool> IsPlaymodeActive => _isPlaymodeActive;
    public Utils.Common.IObservable<bool> IsEditorActive => _isEditormodeActive;

    public EngineRunMode ActiveMode { get; private set; }

    private IntPtr? _enginePtr;
    private readonly IEngineApi.VoidCallbackDelegate _startCallbackDelegate;
    
    private readonly Observable<bool> _isActive = new(false);
    private readonly Observable<bool> _isPlaymodeActive = new(false);
    private readonly Observable<bool> _isEditormodeActive = new(false);
	
    private readonly IEngineApi _engineApi;
    private readonly ILogger<EngineRunner> _logger;
    private readonly IEngineLogger _engineLogger;
    private readonly IEngineWindowController _engineWindowController;
    private readonly IEngineShutdownListener _shutdownListener;
    private readonly IResourceService _resourceService;
    private readonly IClientDllManager _clientDllManager;

    public EngineRunner(
        IEngineApi engineApi,
        ILogger<EngineRunner> logger,
        IEngineLogger engineLogger,
        IEngineWindowController engineWindowController,
        IEngineShutdownListener shutdownListener, 
        IResourceService resourceService, 
        IClientDllManager clientDllManager)
    {
        _engineApi = engineApi;
        _logger = logger;
        _engineLogger = engineLogger;
        _engineWindowController = engineWindowController;
        _shutdownListener = shutdownListener;
        _resourceService = resourceService;
        _clientDllManager = clientDllManager;

        _startCallbackDelegate = HandleEngineStartedEvent;

        _shutdownListener.EngineShutdownEvent += HandleEngineShutdownEvent;
    }

    public async ValueTask DisposeAsync()
    {
        await StopEngine();
        _shutdownListener.EngineShutdownEvent -= HandleEngineShutdownEvent;
    }

    public bool StartEngine(EngineRunMode mode)
    {
        if (_enginePtr != null)
        {
            _logger.LogError("Cannot start playmode because EnginePtr already exists");
            return false;
        }
        
        Task.Run(() =>
        {
            if (!LoadClientDll()) return;
            
            try
            {
                ActiveMode = mode;
                
                _enginePtr = _engineApi.CreateEngine(Path.Combine(_resourceService.GetRootPath(), "bin", "Resources"), mode);

                _engineApi.AddEngineStartCallback(Marshal.GetFunctionPointerForDelegate(_startCallbackDelegate));
                _engineLogger.SubscribeToClient();
                _shutdownListener.SubscribeToClient();
                _engineWindowController.SetupWindow();
                
                _engineApi.Start(_enginePtr.Value);
            }
            catch (Exception e)
            {
                _logger.LogError("Engine failure...");
                _logger.LogException(e);
                _ = StopEngine();
            }
        });

        return true;
    }

    public async Task StopEngine()
    {
        if (!_isActive.Value) return;
        
        try
        {
            if (_enginePtr == null) return;
            
            _engineApi?.Shutdown(_enginePtr.Value, 1);
            _clientDllManager.UnloadDll();
        }
        catch (Exception e)
        {
            _logger.LogError("Could not stop engine");
            _logger.LogException(e);
        }

        while (_isActive.Value)
        {
            await Task.Delay(100);
        }
    }

    private void HandleEngineShutdownEvent(int obj)
    {
        _engineWindowController.DestroyWindow();
        _enginePtr = null;
        
        _isActive.Value = false;
        _isPlaymodeActive.Value = false;
        _isEditormodeActive.Value = false;
    }

    private bool LoadClientDll()
    {
        try
        {
            if (_clientDllManager.DllLoaded.Value)
            {
                _clientDllManager.UnloadDll();
            }
			
            _clientDllManager.LoadDll();

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError("Could not load client dll");
            _logger.LogException(e);
            return false;
        }
    }

    private void HandleEngineStartedEvent()
    {
        Task.Run(() =>
        {
            try
            {
                _isActive.Value = true;
                _isPlaymodeActive.Value = ActiveMode == EngineRunMode.PlayMode;
                _isEditormodeActive.Value = ActiveMode == EngineRunMode.EditorMode;
                EngineStartedEvent?.Invoke();
            }
            catch (Exception e)
            {
                _logger.LogException(e);
            }
        });
    }
}