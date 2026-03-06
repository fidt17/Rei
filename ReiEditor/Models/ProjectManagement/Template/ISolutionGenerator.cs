using System.Collections.Generic;
using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement.Creation;

namespace ReiEditor.Models.ProjectManagement.Template;

public interface ISolutionGenerator
{
	Task<SolutionGenerationResult> GenerateSolution(ProjectCreationConfiguration projectCreationConfiguration);
	Task UpdateProjectFile(string projectFilePath);
	Task AddSourceFiles(string projectFilePath, IEnumerable<string> includes);
}