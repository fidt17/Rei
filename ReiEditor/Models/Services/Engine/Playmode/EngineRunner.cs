using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Engine.Input;
using ReiEditor.Models.Services.Logging.Engine;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Windows.Playmode;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Common.Procedures;

namespace ReiEditor.Models.Services.Engine.Playmode;

public class EngineRunner : IEngineRunner, IAsyncDisposable
{
    public event Action? EngineStartedEvent;
    public event Action? EngineStartFailedEvent;
    
    public Utils.Common.IObservable<bool> IsActive => _isActive;
    public Utils.Common.IObservable<bool> IsPlaymodeActive => _isPlaymodeActive;
    public Utils.Common.IObservable<bool> IsEditorActive => _isEditormodeActive;
    public Utils.Common.IObservable<bool> IsEngineStarting => _isEngineStarting;

    public EngineRunMode ActiveMode { get; private set; }

    private readonly object _lifecycleLock = new();
    private readonly object _nativeCallLock = new();
    private Task _engineCompletion = Task.CompletedTask;
    private Task _startNotification = Task.CompletedTask;
    private IntPtr? _enginePtr;
    private readonly IEngineApi.VoidCallbackDelegate _startCallbackDelegate;
    
    private readonly Observable<bool> _isActive = new(false);
    private readonly Observable<bool> _isPlaymodeActive = new(false);
    private readonly Observable<bool> _isEditormodeActive = new(false);
    private readonly Observable<bool> _isEngineStarting = new(false);
    
    private readonly IEngineApi _engineApi;
    private readonly ILogger<EngineRunner> _logger;
    private readonly IEngineLogger _engineLogger;
    private readonly IEngineInputService _engineInputService;
    private readonly IEngineWindowController _engineWindowController;
    private readonly IEngineShutdownListener _shutdownListener;
    private readonly IResourceService _resourceService;
    private readonly IClientDllManager _clientDllManager;
    private readonly IEditorProceduresService _editorProceduresService;

    private Procedure? _startProcedure;

    public EngineRunner(
        IEngineApi engineApi,
        ILogger<EngineRunner> logger,
        IEngineLogger engineLogger,
        IEngineInputService engineInputService,
        IEngineWindowController engineWindowController,
        IEngineShutdownListener shutdownListener, 
        IResourceService resourceService, 
        IClientDllManager clientDllManager,
        IEditorProceduresService editorProceduresService)
    {
        _engineApi = engineApi;
        _logger = logger;
        _engineLogger = engineLogger;
        _engineInputService = engineInputService;
        _engineWindowController = engineWindowController;
        _shutdownListener = shutdownListener;
        _resourceService = resourceService;
        _clientDllManager = clientDllManager;
        _editorProceduresService = editorProceduresService;

        _startCallbackDelegate = HandleEngineStartedEvent;
    }

    public async ValueTask DisposeAsync()
    {
        await StopEngine();
    }

    public bool StartEngine(EngineRunMode mode)
    {
        lock (_lifecycleLock)
        {
            if (!_engineCompletion.IsCompleted)
            {
                _logger.LogError("Cannot start engine while its previous lifecycle is still running");
                return false;
            }

            // Reserve the lifecycle before scheduling: two callers must never load the same DLL concurrently.
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _engineCompletion = completion.Task;
            _startNotification = Task.CompletedTask;
            BeginStartProcedure();
            _isEngineStarting.Value = true;
            Task.Run(() => RunEngine(mode, completion));
            return true;
        }
    }

    private void RunEngine(EngineRunMode mode, TaskCompletionSource completion)
    {
        var enginePtr = IntPtr.Zero;
        var startFailed = false;
        try
        {
            if (!LoadClientDll())
            {
                startFailed = true;
                return;
            }

            ActiveMode = mode;
            enginePtr = _engineApi.CreateEngine(Path.Combine(_resourceService.GetRootPath(), ResourceConstants.BIN_DIR_NAME, ResourceConstants.RESOURCES_DIR_NAME), mode);
            lock (_nativeCallLock) _enginePtr = enginePtr;
            _engineApi.AddEngineStartCallback(Marshal.GetFunctionPointerForDelegate(_startCallbackDelegate));
            _engineLogger.SubscribeToClient();
            _engineInputService.SubscribeToClient();
            _shutdownListener.SubscribeToClient();
            _engineWindowController.SetupWindow();
            _engineApi.Start(enginePtr);
        }
        catch (Exception e)
        {
            startFailed = true;
            _logger.LogError("Engine failure...");
            _logger.LogException(e);
        }
        finally
        {
            // Drain the queued start callback so it cannot reactivate a stopped/new engine.
            _startNotification.GetAwaiter().GetResult();
            // Shutdown may still be returning through the project DLL after Start exits.
            // Keep that native call, destruction and FreeLibrary mutually exclusive.
            lock (_nativeCallLock)
            {
                try
                {
                    _engineWindowController.DestroyWindow();
                }
                catch (Exception e)
                {
                    _logger.LogException(e);
                }

                if (enginePtr != IntPtr.Zero)
                {
                    try
                    {
                        _engineApi.DestroyEngine(enginePtr);
                    }
                    catch (Exception e)
                    {
                        _logger.LogException(e);
                    }
                }
                _enginePtr = null;
                try
                {
                    if (_clientDllManager.DllLoaded.Value) _clientDllManager.UnloadDll();
                }
                catch (Exception e)
                {
                    _logger.LogException(e);
                }
            }

            if (startFailed) _engineApi.MarkEngineStopped();
            lock (_lifecycleLock)
            {
                _isActive.Value = false;
                _isPlaymodeActive.Value = false;
                _isEditormodeActive.Value = false;
                _isEngineStarting.Value = false;
                EndStartProcedure();
                completion.TrySetResult();
            }
            if (startFailed) EngineStartFailedEvent?.Invoke();
        }
    }

    public async Task StopEngine()
    {
        Task completion;
        lock (_lifecycleLock) completion = _engineCompletion;
        while (!completion.IsCompleted)
        {
            lock (_nativeCallLock)
            {
                if (completion.IsCompleted) break;
                if (_enginePtr is { } pointer && _isActive.Value && _engineApi.IsEngineRunning)
                {
                    try
                    {
                        _engineApi.Shutdown(pointer, 1);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("Could not stop engine");
                        _logger.LogException(e);
                    }
                    break;
                }
            }
            // A stop during startup must wait for either readiness or startup failure.
            await Task.WhenAny(completion, Task.Delay(25));
        }
        await completion;
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
        _startNotification = Task.Run(() =>
        {
            try
            {
                _isActive.Value = true;
                _isPlaymodeActive.Value = ActiveMode == EngineRunMode.PlayMode;
                _isEditormodeActive.Value = ActiveMode == EngineRunMode.EditorMode;
                _isEngineStarting.Value = false;
                
                EngineStartedEvent?.Invoke();
            }
            catch (Exception e)
            {
                _logger.LogException(e);
            }
            finally
            {
                EndStartProcedure();
            }
        });
    }

    private void BeginStartProcedure()
    {
        if (_startProcedure != null) return;
        _startProcedure = new Procedure("Engine starting");
        _editorProceduresService.TrackProcedure(_startProcedure);
    }

    private void EndStartProcedure()
    {
        if (_startProcedure == null) return;
        _startProcedure.Complete();
        _startProcedure = null;
    }
}
