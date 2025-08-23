using System;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Logging.Engine;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Windows.Playmode;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Engine.Playmode;

public class EngineRunner : IEngineRunner, IDisposable
{
    public event Action? EngineShutdownEvent;

    public Utils.Common.IObservable<bool> IsActive => _isActive;
    public Utils.Common.IObservable<bool> IsPlaymodeActive => _isPlaymodeActive;
    
    public EngineRunMode ActiveMode { get; private set; }

    private IntPtr? _enginePtr;
    
    private readonly Observable<bool> _isActive = new(false);
    private readonly Observable<bool> _isPlaymodeActive = new(false);
	
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

        _shutdownListener.EngineShutdownEvent += HandleEngineShutdownEvent;
        
        _isPlaymodeActive.Subscribe(x => _logger.LogWarning($"playmode active: {x}"), false);
    }
    
    public void Dispose()
    {
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
                
                _enginePtr = _engineApi.CreateEngine(Path.Combine(_resourceService.GetRootPath(), "bin", "Resources"));

                _engineLogger.SubscribeToClient();
                _shutdownListener.SubscribeToClient();
                _engineWindowController.SetupWindow();
                
                _isActive.Value = true;
                _isPlaymodeActive.Value = ActiveMode == EngineRunMode.PlayMode;
                
                _engineApi.Start(_enginePtr.Value);
            }
            catch (Exception e)
            {
                _logger.LogError("Engine failure...");
                _logger.LogException(e);
                StopEngine();
            }
        });

        return true;
    }

    public void StopEngine()
    {
        try
        {
            if (_enginePtr == null) return;
            
            _isActive.Value = false;
            _isPlaymodeActive.Value = false;

            _engineApi?.Shutdown(_enginePtr.Value, 1);
            _clientDllManager.UnloadDll();
        }
        catch (Exception e)
        {
            _logger.LogError("Could not stop engine");
            _logger.LogException(e);
        }
    }

    private void HandleEngineShutdownEvent(int obj)
    {
        _isActive.Value = false;
        _isPlaymodeActive.Value = false;
        
        _engineWindowController.DestroyWindow();
        _enginePtr = null;
        
        EngineShutdownEvent?.Invoke();
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
}