using System;
using ReiEditor.Models.Services.Build;
using ReiEditor.Utils.Common.Condition;

namespace ReiEditor.Models.Services.Engine.Playmode;

public class PlaymodeStarter : IPlaymodeStarter, IDisposable
{
    public ICondition CanStart => _canStartPlaymodeCondition;

    private readonly ConditionGroup _canStartPlaymodeCondition;
    private readonly IEngineRunner _engineRunner;

    public PlaymodeStarter(IBuildService buildService, IEngineRunner engineRunner)
    {
        _engineRunner = engineRunner;

        _canStartPlaymodeCondition = new ConditionGroup(
            new Condition(_engineRunner.IsPlaymodeActive, target: false),
            new Condition(buildService.IsBuildReady, target: true));
    }

    public void Dispose()
    {
        _canStartPlaymodeCondition.Dispose();
    }

    public bool TryStart()
    {
        if (!CanStart.IsTrue.Value) return false;
        return _engineRunner.StartEngine(EngineRunMode.PlayMode);
    }
}
