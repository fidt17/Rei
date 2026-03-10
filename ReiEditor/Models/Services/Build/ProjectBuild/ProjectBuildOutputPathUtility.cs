using System.IO;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Resources.Client;

namespace ReiEditor.Models.Services.Build.ProjectBuild;

public class ProjectBuildOutputPathUtility
{
    private readonly IActiveProjectService _activeProjectService;
    private readonly IResourceService _resourceService;

    public ProjectBuildOutputPathUtility(IActiveProjectService activeProjectService, IResourceService resourceService)
    {
        _activeProjectService = activeProjectService;
        _resourceService = resourceService;
    }

    public string GetDefaultPackageOutputPath(BuildConfigurationEnum configuration)
    {
        var project = _activeProjectService.GetActiveProject();
        var folderName = configuration == BuildConfigurationEnum.Release ? "Release Build" : "Debug Build";
        return Path.Combine(project.GetDirectoryPath(), "Builds", folderName);
    }

    public string GetBuildOutputDirectory(BuildConfigurationEnum configuration)
    {
        var project = _activeProjectService.GetActiveProject();
        var configName = configuration == BuildConfigurationEnum.Release ? "x64Release" : "x64Debug";
        return _resourceService.GetRootPath(ResourceConstants.BIN_DIR_NAME, configName, project.ProjectName);
    }

    public string GetBuildOutputExePath(BuildConfigurationEnum configuration)
    {
        var project = _activeProjectService.GetActiveProject();
        return Path.Combine(GetBuildOutputDirectory(configuration), $"{project.ProjectName}.exe");
    }

    public string GetResourcesDirectory()
    {
        return _resourceService.GetRootPath(ResourceConstants.BIN_DIR_NAME, ResourceConstants.RESOURCES_DIR_NAME);
    }
}
