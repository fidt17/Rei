using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement.Template;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.ProjectManagement.Update;

public class ProjectUpdateService : IProjectUpdateService
{
    private readonly ILogger<ProjectUpdateService> _logger;
    private readonly ISolutionGenerator _solutionGenerator;

    public ProjectUpdateService(ILogger<ProjectUpdateService> logger, ISolutionGenerator solutionGenerator)
    {
        _logger = logger;
        _solutionGenerator = solutionGenerator;
    }

    public async Task UpdateProject(Project project)
    {
        _logger.Log("Updating visual studio project file");
        await _solutionGenerator.UpdateProjectFile(project.ProjectVisualStudioProjectPath);
    }
}