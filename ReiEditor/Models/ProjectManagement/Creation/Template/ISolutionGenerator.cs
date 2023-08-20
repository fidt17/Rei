using System.Threading.Tasks;

namespace ReiEditor.Models.ProjectManagement.Creation.Template;

public interface ISolutionGenerator
{
	Task<string> GenerateSolution(ProjectCreationConfiguration projectCreationConfiguration);
}