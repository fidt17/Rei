using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common.Condition;

namespace ReiEditor.Models.Services.Engine.Playmode;

public class EditorModeStarter : IEditorModeStarter, IDisposable
{
    public ICondition CanStart => _canStartCondition;
    	
    private ConditionGroup _canStartCondition { get; }
    	
    private readonly IBuildService _buildService;
    private readonly ILogger<EditorModeStarter> _logger;
    private readonly IEngineRunner _engineRunner;
    
    public EditorModeStarter(
        IBuildService buildService,
        ILogger<EditorModeStarter> logger,
        IEngineRunner engineRunner)
    {
        _buildService = buildService;
        _logger = logger;
        _engineRunner = engineRunner;

        _canStartCondition = new ConditionGroup(
            new Condition(_engineRunner.IsEditorActive, target: false),
            new Condition(_buildService.IsBuildReady, target: true));
        
        _engineRunner.IsPlaymodeActive.Subscribe(HandleIsPlaymodeActiveValueChangedEvent, invoke: false);
    }

    public void Dispose()
    {
        _canStartCondition.Dispose();
        
        _engineRunner.IsPlaymodeActive.Unsubscribe(HandleIsPlaymodeActiveValueChangedEvent);
    }

    public void Start()
    {
        if (_engineRunner.IsEditorActive.Value) return;
        
        Task.Run(EnterEditormodeTask);
    }

    private async Task EnterEditormodeTask()
    {
        try
        {
            await _engineRunner.StopEngine();
        
            if (!CanStart.IsTrue.Value)
            {
                _logger.LogError($"Cannot start editor mode");
                return;
            }
    
            _engineRunner.StartEngine(EngineRunMode.EditorMode);
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }
    }

    private void HandleIsPlaymodeActiveValueChangedEvent(bool isActive)
    {
        if (isActive) return;
        
        Start();
    }
}