using System;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement.Creation;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.ProjectManagement.Template;

public class SolutionGenerator : ISolutionGenerator
{
    private readonly ILogger<SolutionGenerator> _logger;
    private readonly IProjectTemplateProvider _templateProvider;
    private readonly IEngineSettingsProvider _engineSettingsProvider;

    public SolutionGenerator(ILogger<SolutionGenerator> logger, IProjectTemplateProvider templateProvider, IEngineSettingsProvider engineSettingsProvider)
    {
        _logger = logger;
        _templateProvider = templateProvider;
        _engineSettingsProvider = engineSettingsProvider;
    }

    public async Task<SolutionGenerationResult> GenerateSolution(ProjectCreationConfiguration config)
    {
        var solutionDirPath = config.FullPath;
        var projectDirPath = Path.Combine(solutionDirPath, "Project/Scripts");
			
        _logger.Log($"Creating solution directory at: {solutionDirPath}");
        Directory.CreateDirectory(solutionDirPath);

        _logger.Log($"Creating project directory at: {projectDirPath}");
        Directory.CreateDirectory(projectDirPath);

        var solutionGuid = Guid.NewGuid();
        var projectGuid = Guid.NewGuid();
			
        var solutionFilePath = await CreateSolutionFile(config.ProjectName, solutionDirPath, solutionGuid, projectGuid);
        var projectFilePath = await CreateProjectFile(config.ProjectName, projectDirPath, projectGuid);
        await CreateSourceFiles(projectDirPath);

        return new SolutionGenerationResult
        {
            SolutionPath = solutionFilePath,
            ProjectPath = projectFilePath
        };
    }

    public async Task UpdateProjectFile(string projectFilePath)
    {
        _logger.Log("Updating VS project file");

        var projectFile = await File.ReadAllTextAsync(projectFilePath);
        if (projectFile == null) throw new Exception($"Missing project file. Path: {projectFilePath}");

        var projectGuid = GetProjectGuid(projectFile);
        var projectName = GetProjectName(projectFile);
		
        var projectTemplate = await _templateProvider.GetVSProjectTemplate();
        var filledTemplate = string.Format(
            projectTemplate, 
            projectGuid, projectName, 
            _engineSettingsProvider.GetEngineDebugIncludeDir(), 
            _engineSettingsProvider.GetEngineReleaseIncludeDir(), 
            _engineSettingsProvider.GetEngineSourceIncludes(),
            GetMainFileName());
		
        await File.WriteAllTextAsync(projectFilePath, filledTemplate);
    }

    public async Task AddClCompile(string projectFilePath, string includePath)
    {
        if (string.IsNullOrWhiteSpace(includePath)) return;
        
        if (includePath[0] == '\\' || includePath[0] == '/')
        {
            includePath = includePath.Remove(0, 1);
        }

        includePath = includePath.Replace("\\", "/");
        
        var projectFile = await File.ReadAllTextAsync(projectFilePath);
        if (projectFile == null) throw new Exception($"Missing project file. Path: {projectFilePath}");

        if (projectFile.Contains(includePath)) return;

        const string GROUP = "<ItemGroup Label=\"ClCompile\">";
        var groupIdx = projectFile.IndexOf(GROUP, StringComparison.Ordinal) + GROUP.Length;

        projectFile = projectFile.Insert(groupIdx, $"\n   <ClCompile Include=\"{includePath}\" />\n");
        await File.WriteAllTextAsync(projectFilePath, projectFile);
    }

    private async Task<string> CreateSolutionFile(string projectName, string solutionFolderPath, Guid solutionGuid, Guid projectGuid)
    {
        _logger.Log("Creating solution file");
		
        var solutionTemplate = await _templateProvider.GetVSSolutionTemplate();
        var filledTemplate = string.Format(
            solutionTemplate, 
            projectName, 
            FormatGuid(projectGuid), 
            FormatGuid(solutionGuid));
		
        var filePath = Path.Combine(solutionFolderPath, $"{projectName}{FileExtensions.VS_SOLUTION}");
		
        await File.WriteAllTextAsync(filePath, filledTemplate);

        return filePath;
    }

    private async Task<string> CreateProjectFile(string projectName, string projectFolderPath, Guid projectGuid)
    {
        _logger.Log("Creating VS project file");
		
        var projectTemplate = await _templateProvider.GetVSProjectTemplate();
        var filledTemplate = string.Format(
            projectTemplate, 
            FormatGuid(projectGuid), projectName, 
            _engineSettingsProvider.GetEngineDebugIncludeDir(), 
            _engineSettingsProvider.GetEngineReleaseIncludeDir(), 
            _engineSettingsProvider.GetEngineSourceIncludes(),
            GetMainFileName());
		
        var filePath = Path.Combine(projectFolderPath, $"{projectName}{FileExtensions.VS_PROJECT}");
		
        await File.WriteAllTextAsync(filePath, filledTemplate);

        return filePath;
    }

    private async Task CreateSourceFiles(string projectFolderPath)
    {
        _logger.Log("Creating source files");

        var template = await _templateProvider.GetMainFileTemplate();

        var filePath = Path.Combine(projectFolderPath, GetMainFileName());
        await File.WriteAllTextAsync(filePath, template);
    }

    private static string GetMainFileName() => "ReiApp.cpp";
    private static string FormatGuid(Guid guid) => "{" + guid + "}";

    private static string GetProjectGuid(string projectFile)
    {
        var guid = StringExtensions.GetValueFromXml(projectFile, "ProjectGuid");
        if (string.IsNullOrWhiteSpace(guid)) throw new Exception("Could not find project guid");
        
        return guid;
    }
	
    private static string GetProjectName(string projectFile)
    {
        var name = StringExtensions.GetValueFromXml(projectFile, "RootNamespace");
        if (string.IsNullOrWhiteSpace(name)) throw new Exception("Could not find project name");
        
        return name;
    }
}