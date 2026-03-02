using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;
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
    private readonly IAssetImporter _assetImporter;
    private readonly ILogger<BuildStarter> _logger;

    private bool _commandInProgress;
    
    public BuildStarter(
        IBuildService buildService,
        IAssetsService assetsService,
        IEngineRunner engineRunner,
        IClientDllManager dllManager,
        IAssetImporter assetImporter,
        ILogger<BuildStarter> logger)
    {
        _buildService = buildService;
        _assetsService = assetsService;
        _engineRunner = engineRunner;
        _dllManager = dllManager;
        _assetImporter = assetImporter;
        _logger = logger;

        _canStartBuildCondition = new ConditionGroup(
            new Condition(_buildService.BuildInProgress, target: false),
            new Condition(_engineRunner.IsPlaymodeActive, target: false),
            new Condition(_engineRunner.IsEngineStarting, target: false),
            new Condition(_assetsService.SaveInProcess, target: false),
            new Condition(_assetImporter.IsImporting, target: false));
    }

    public void Dispose()
    {
        _canStartBuildCondition.Dispose();
    }

    public async Task<bool> BuildProject(BuildConfigurationEnum configuration)
    {
        if (_commandInProgress) return false;
        _commandInProgress = true;
        
        try
        {
            if (!_canStartBuildCondition.IsTrue.Value) return false;
            
            await _engineRunner.StopEngine();
            _dllManager.UnloadDll();
            await Task.Delay(250);
            if (!_canStartBuildCondition.IsTrue.Value) return false;

            await _assetsService.SaveProject();
            if (!_canStartBuildCondition.IsTrue.Value) return false;
            
            return await _buildService.BuildProject(configuration);
        }
        catch (Exception e)
        {
            _logger.LogException(e);
            return false;
        }
        finally
        {
            _commandInProgress = false;
        }
    }
}
