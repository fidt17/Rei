using System;
using System.IO;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Resources;

namespace ReiEditor.Models.Services.Build;

public class EditorBuildOutputService : IEditorBuildOutputService
{
    private const string STAGING_FOLDER_NAME = ".rei_tmp";
    private const string STAGING_BUILD_FOLDER_NAME = "editor_build_staging";
    private const string EDITOR_CONFIGURATION_FOLDER_NAME = "x64EditorDebug";

    private readonly IActiveProjectService _activeProjectService;

    public EditorBuildOutputService(IActiveProjectService activeProjectService)
    {
        _activeProjectService = activeProjectService;
    }

    public EditorBuildOutput GetLiveOutput()
    {
        var project = _activeProjectService.GetActiveProject();
        var projectRoot = project.GetDirectoryPath();
        var binDirectory = Path.Combine(projectRoot, ResourceConstants.BIN_DIR_NAME);
        var clientOutputDirectory = Path.Combine(binDirectory, EDITOR_CONFIGURATION_FOLDER_NAME, project.ProjectName);
        return new EditorBuildOutput(
            projectRoot,
            binDirectory,
            clientOutputDirectory,
            Path.Combine(clientOutputDirectory, $"{project.ProjectName}.dll"),
            Path.Combine(binDirectory, ResourceConstants.RESOURCES_DIR_NAME));
    }

    public EditorBuildOutput PrepareStagingOutput()
    {
        var liveOutput = GetLiveOutput();
        CleanupStagingOutput();

        var stagingRoot = Path.Combine(liveOutput.StagingRootPath, STAGING_FOLDER_NAME, STAGING_BUILD_FOLDER_NAME);
        var binDirectory = Path.Combine(stagingRoot, ResourceConstants.BIN_DIR_NAME);
        var clientOutputDirectory = Path.Combine(binDirectory, EDITOR_CONFIGURATION_FOLDER_NAME, Path.GetFileName(liveOutput.ClientOutputDirectoryPath));
        var resourcesDirectory = Path.Combine(binDirectory, ResourceConstants.RESOURCES_DIR_NAME);

        Directory.CreateDirectory(clientOutputDirectory);
        Directory.CreateDirectory(resourcesDirectory);

        return new EditorBuildOutput(
            stagingRoot,
            binDirectory,
            clientOutputDirectory,
            Path.Combine(clientOutputDirectory, Path.GetFileName(liveOutput.ClientDllPath)),
            resourcesDirectory);
    }

    public void PromoteStagingOutput(EditorBuildOutput stagingOutput)
    {
        var liveOutput = GetLiveOutput();

        ValidateStagingOutput(stagingOutput);

        ReplaceDirectory(stagingOutput.ClientOutputDirectoryPath, liveOutput.ClientOutputDirectoryPath);
        ReplaceDirectory(stagingOutput.ResourcesDirectoryPath, liveOutput.ResourcesDirectoryPath);
    }

    public void CleanupStagingOutput()
    {
        var liveOutput = GetLiveOutput();
        var stagingRoot = Path.Combine(liveOutput.StagingRootPath, STAGING_FOLDER_NAME, STAGING_BUILD_FOLDER_NAME);
        if (!Directory.Exists(stagingRoot)) return;

        Directory.Delete(stagingRoot, recursive: true);
    }

    private static void ValidateStagingOutput(EditorBuildOutput stagingOutput)
    {
        if (!Directory.Exists(stagingOutput.ClientOutputDirectoryPath))
            throw new Exception($"Staged client output is missing: {stagingOutput.ClientOutputDirectoryPath}");

        if (!File.Exists(stagingOutput.ClientDllPath))
            throw new Exception($"Staged client dll is missing: {stagingOutput.ClientDllPath}");

        if (!Directory.Exists(stagingOutput.ResourcesDirectoryPath))
            throw new Exception($"Staged resources output is missing: {stagingOutput.ResourcesDirectoryPath}");
    }

    private static void ReplaceDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        CopyDirectory(source, target);
    }

    private static void CopyDirectory(string source, string target)
    {
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(source, target));
        }

        foreach (var sourceFile in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var targetFile = sourceFile.Replace(source, target);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(sourceFile, targetFile, overwrite: true);
        }
    }
}
