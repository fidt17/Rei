using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Utils.Common.Condition;

namespace ReiEditor.Models.Services.Build;

public class BuildStarter : IBuildStarter, IDisposable
{
	public ICondition CanStartBuild => _canStartBuildCondition;

	private readonly ConditionGroup _canStartBuildCondition;
	
	private readonly IBuildService _buildService;
	private readonly IClientDllManager _dllManager;
	private readonly IPlaymodeService _playmodeService;

	public BuildStarter(IBuildService buildService, IClientDllManager dllManager, IPlaymodeService playmodeService)
	{
		_buildService = buildService;
		_dllManager = dllManager;
		_playmodeService = playmodeService;

		_canStartBuildCondition = new ConditionGroup(
			new Condition(_buildService.BuildInProgress, target: false),
			new Condition(_dllManager.DllLoaded, target: false),
			new Condition(_playmodeService.IsPlaymodeActive, target: false));
	}

	public void Dispose()
	{
		_canStartBuildCondition.Dispose();
	}

	public Task<bool> BuildProject(BuildConfigurationEnum configuration) => _buildService.BuildProject(configuration);
}