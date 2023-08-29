using System;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Build;

public interface IBuildService
{
	event Action<bool> CanStartBuildChangedEvent;
	event Action BuildStartedEvent;
	event Action BuildFinishedEvent;
	
	bool BuildInProgress { get; }
	bool CanStartBuild { get; }
	
	Task<bool> BuildProject(BuildConfigurationEnum configuration);
}