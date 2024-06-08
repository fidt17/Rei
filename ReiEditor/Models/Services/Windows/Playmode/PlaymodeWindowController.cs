using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Windows.Playmode;

public class PlaymodeWindowController : IPlaymodeWindowController
{
    public Utils.Common.IObservable<IntPtr?> WindowPointer => _windowPointer;

    private readonly Observable<IntPtr?> _windowPointer = new(null);
    
    private readonly IEngineApi _engineApi;
    private readonly ILogger<PlaymodeWindowController> _logger;

    public PlaymodeWindowController(IEngineApi engineApi, ILogger<PlaymodeWindowController> logger)
    {
        _engineApi = engineApi;
        _logger = logger;
    }

    public void SetupWindow()
    {
        try
        {
            Task.Run(_engineApi.CreatePlaymodeWindow).ContinueWith(task =>
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
    }
}