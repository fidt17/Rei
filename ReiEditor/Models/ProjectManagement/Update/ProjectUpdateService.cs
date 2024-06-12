using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement.Template;
using ReiEditor.Models.Resources.EngineResources;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.ProjectManagement.Update;

public class ProjectUpdateService : IProjectUpdateService
{
    private readonly ILogger<ProjectUpdateService> _logger;
    private readonly ISolutionGenerator _solutionGenerator;
    private readonly IEngineResourcesImporter _engineResourcesImporter;

    public ProjectUpdateService(
        ILogger<ProjectUpdateService> logger, 
        ISolutionGenerator solutionGenerator, 
        IEngineResourcesImporter engineResourcesImporter)
    {
        _logger = logger;
        _solutionGenerator = solutionGenerator;
        _engineResourcesImporter = engineResourcesImporter;
    }

    public async Task UpdateProject(Project project)
    {
        _logger.Log("Updating visual studio project file");
        await _solutionGenerator.UpdateProjectFile(project.ProjectVisualStudioProjectPath);
        await _engineResourcesImporter.Import();
    }
}