using System;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Build;

public interface IBuildService
{
	event Action<bool> CanStartBuildChangedEvent;
	
	bool CanStartBuild { get; }
	
	Task<bool> BuildProject(BuildConfigurationEnum configuration);
}