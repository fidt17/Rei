using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Utils.Common.Condition;

namespace ReiEditor.Models.Services.Build;

public class BuildStarter : IBuildStarter, IDisposable
{
    public ICondition CanStartBuild => _canStartBuildCondition;

    private readonly ConditionGroup _canStartBuildCondition;
	
    private readonly IBuildService _buildService;
    private readonly IAssetsService _assetsService;
    private readonly IEngineRunner _engineRunner;
    private readonly IClientDllManager _dllManager;

    public BuildStarter(IBuildService buildService, IAssetsService assetsService, IEngineRunner engineRunner, IClientDllManager dllManager)
    {
        _buildService = buildService;
        _assetsService = assetsService;
        _engineRunner = engineRunner;
        _dllManager = dllManager;

        _canStartBuildCondition = new ConditionGroup(
            new Condition(_buildService.BuildInProgress, target: false),
            new Condition(_engineRunner.IsPlaymodeActive, target: false),
            new Condition(_assetsService.SaveInProcess, target: false));
    }

    public void Dispose()
    {
        _canStartBuildCondition.Dispose();
    }

    public async Task<bool> BuildProject(BuildConfigurationEnum configuration)
    {
        await _engineRunner.StopEngine();
        _dllManager.UnloadDll();
        
        if (!_canStartBuildCondition.IsTrue.Value) return false;

        await _assetsService.SaveProject();
        return await _buildService.BuildProject(configuration);
    }
}