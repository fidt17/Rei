using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common.Condition;

namespace ReiEditor.Models.Services.Engine.Playmode;

public class PlaymodeStarter : IPlaymodeStarter, IDisposable
{
    public ICondition CanStart => _canStartPlaymodeCondition;
	
    private ConditionGroup _canStartPlaymodeCondition { get; }
	
    private readonly IBuildService _buildService;
    private readonly ILogger<PlaymodeStarter> _logger;
    private readonly IBuildStarter _buildStarter;
    private readonly IEngineRunner _engineRunner;

    public PlaymodeStarter(
        IBuildService buildService,
        ILogger<PlaymodeStarter> logger,
        IBuildStarter buildStarter,
        IEngineRunner engineRunner)
    {
        _buildService = buildService;
        _logger = logger;
        _buildStarter = buildStarter;
        _engineRunner = engineRunner;

        _canStartPlaymodeCondition = new ConditionGroup(
            new Condition(_engineRunner.IsPlaymodeActive, target: false),
            new Condition(_buildService.IsBuildReady, target: true));
    }

    public void Dispose()
    {
        _canStartPlaymodeCondition.Dispose();
    }
	
    public void Start()
    {
        Task.Run(EnterPlaymodeTask);
    }

    private async Task EnterPlaymodeTask()
    {
        try
        {
            await _engineRunner.StopEngine();
        
            await _buildStarter.BuildProject(BuildConfigurationEnum.EditorDebug);
        
            if (!CanStart.IsTrue.Value)
            {
                _logger.LogError($"Cannot start playmode");
                return;
            }

            _engineRunner.StartEngine(EngineRunMode.PlayMode);
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }
    }
}