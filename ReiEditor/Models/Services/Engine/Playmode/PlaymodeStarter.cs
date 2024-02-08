using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common.Condition;

namespace ReiEditor.Models.Services.Engine.Playmode;

public class PlaymodeStarter : IPlaymodeStarter, IDisposable
{
    public ICondition CanStartPlaymode => _canStartPlaymodeCondition;
	
    private ConditionGroup _canStartPlaymodeCondition { get; }
	
    private readonly IBuildService _buildService;
    private readonly IPlaymodeService _playmodeService;
    private readonly ILogger<PlaymodeStarter> _logger;
    private readonly IBuildStarter _buildStarter;

    public PlaymodeStarter(IBuildService buildService, IPlaymodeService playmodeService, ILogger<PlaymodeStarter> logger, IBuildStarter buildStarter)
    {
        _buildService = buildService;
        _playmodeService = playmodeService;
        _logger = logger;
        _buildStarter = buildStarter;

        _canStartPlaymodeCondition = new ConditionGroup(
            new Condition(_playmodeService.IsPlaymodeActive, target: false),
            new Condition(_buildService.IsBuildReady, target: true));
    }

    public void Dispose()
    {
        _canStartPlaymodeCondition.Dispose();
    }
	
    public void StartPlaymode()
    {
        Task.Run(EnterPlaymodeTask);
    }

    private async Task EnterPlaymodeTask()
    {
        if (!CanStartPlaymode.IsTrue.Value)
        {
            _logger.LogError($"Cannot start playmode");
            return;
        }

        await _buildStarter.BuildProject(BuildConfigurationEnum.EditorDebug);
        _playmodeService.StartPlaymode();
    }
}