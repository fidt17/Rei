using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement.Template;
using ReiEditor.Models.Resources.EngineResources;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Import;

namespace ReiEditor.Models.ProjectManagement.Update;

public class ProjectUpdateService : IProjectUpdateService
{
    private readonly ISolutionGenerator _solutionGenerator;
    private readonly IEngineResourcesImporter _engineResourcesImporter;
    private readonly IAssetImporter _assetImporter;

    public ProjectUpdateService(
        ISolutionGenerator solutionGenerator, 
        IEngineResourcesImporter engineResourcesImporter, 
        IAssetImporter assetImporter)
    {
        _solutionGenerator = solutionGenerator;
        _engineResourcesImporter = engineResourcesImporter;
        _assetImporter = assetImporter;
    }

    public async Task UpdateProject(Project project)
    {
        await _solutionGenerator.UpdateProjectFile(project.ProjectVisualStudioProjectPath);
        await _engineResourcesImporter.Import();
        await _assetImporter.ReimportAll();
    }
}