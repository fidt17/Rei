using System.Threading.Tasks;

namespace ReiEditor.Models.ProjectManagement.Setup;

public interface IProjectSetupService
{
	Task PrepareProject();
}