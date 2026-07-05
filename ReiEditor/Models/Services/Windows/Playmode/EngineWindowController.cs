using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Windows.Playmode;

public class EngineWindowController : IEngineWindowController
{
    public Utils.Common.IObservable<IntPtr?> WindowPointer => _windowPointer;
    public Utils.Common.IObservable<(int Width, int Height)?> ViewportSize => _viewportSize;

    private readonly Observable<IntPtr?> _windowPointer = new(null);
    private readonly Observable<(int Width, int Height)?> _viewportSize = new(null);
    
    private readonly IEngineApi _engineApi;
    private readonly ILogger<EngineWindowController> _logger;

    public EngineWindowController(IEngineApi engineApi, ILogger<EngineWindowController> logger)
    {
        _engineApi = engineApi;
        _logger = logger;
    }

    public void SetupWindow()
    {
        try
        {
            Task.Run(_engineApi.CreateEngineWindow).ContinueWith(task =>
            {
                _windowPointer.Value = task.Result;
            });
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }
    }

    public void DestroyWindow()
    {
        _windowPointer.Value = null;
        _viewportSize.Value = null;
    }

    public void SetViewportSize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        _viewportSize.Value = (width, height);
    }
}
