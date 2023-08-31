using System.Threading.Tasks;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Build;

public interface IBuildService
{
	Observable<bool> BuildInProgress { get; }
	Observable<bool> IsBuildReady { get; }
	
	Task<bool> BuildProject(BuildConfigurationEnum configuration);
}