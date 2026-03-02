using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Import;
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
    
    private bool _commandInProgress;
    
    public EditorModeStarter(
        IBuildService buildService,
        ILogger<EditorModeStarter> logger,
        IAssetsService assetsService,
        IAssetImporter assetImporter,
        IEngineRunner engineRunner)
    {
        _buildService = buildService;
        _logger = logger;
        _engineRunner = engineRunner;

        _canStartCondition = new ConditionGroup(
            new Condition(_buildService.BuildInProgress, target: false),
            new Condition(_engineRunner.IsEditorActive, target: false),
            new Condition(_engineRunner.IsEngineStarting, target: false),
            new Condition(assetsService.SaveInProcess, target: false),
            new Condition(assetImporter.IsImporting, target: false),
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
        Task.Run(EnterEditormodeTask);
    }

    private async Task EnterEditormodeTask()
    {
        if (_commandInProgress) return;
        _commandInProgress = true;
        
        try
        {
            await Task.Delay(250);
            if (!CanStart.IsTrue.Value) return;
            
            await _engineRunner.StopEngine();

            if (!CanStart.IsTrue.Value) return;
    
            _engineRunner.StartEngine(EngineRunMode.EditorMode);
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }
        finally
        {
            _commandInProgress = false;
        }
    }

    private void HandleIsPlaymodeActiveValueChangedEvent(bool isActive)
    {
        if (isActive) return;
        
        Start();
    }
}
