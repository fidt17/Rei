using System.Threading.Tasks;
using ReiEditor.Utils.Common.Condition;

namespace ReiEditor.Models.Services.Build;

public interface IBuildStarter
{
	ICondition CanStartBuild { get; }

	Task<bool> BuildProject(BuildConfigurationEnum configurationEnum);
}