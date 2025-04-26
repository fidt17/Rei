using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        _logger.Log("Updating Visual Studio project file");

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
            _engineSettingsProvider.GetEngineSourceIncludes());
		
        await File.WriteAllTextAsync(projectFilePath, filledTemplate);
    }

    public async Task AddSourceFiles(string projectFilePath, IEnumerable<string> includes)
    {
        var projectFile = await File.ReadAllTextAsync(projectFilePath);
        if (projectFile == null) throw new Exception($"Missing project file. Path: {projectFilePath}");
        
        var includesList = includes.ToList();

        for (var index = includesList.Count - 1; index >= 0; index--)
        {
            var include = includesList[index];
            if (include == null)
            {
                _logger.LogError("Missing include path");
                includesList.RemoveAt(index);
                continue;
            }
            
            include = include.Replace("/", "\\");

            if (include[0] == '\\')
            {
                include = include.Remove(0, 1);
            }

            if (projectFile.Contains(include))
            {
                includesList.RemoveAt(index);
                continue;
            }

            includesList[index] = include;
        }

        var compileStr = new StringBuilder();
        var includeStr = new StringBuilder();
        foreach (var s in includesList)
        {
            if (s.EndsWith(".cpp"))
            {
                compileStr.AppendLine($"   <ClCompile Include=\"{s}\" />");
            }
            else if (s.EndsWith(".h"))
            {
                includeStr.AppendLine($"   <ClInclude Include=\"{s}\" />");
            }
        }
        
        const string COMPILE_GROUP = "<ItemGroup Label=\"ClCompile\">";
        var compileGroupIdx = projectFile.IndexOf(COMPILE_GROUP, StringComparison.Ordinal) + COMPILE_GROUP.Length;
        projectFile = projectFile.Insert(compileGroupIdx, $"\n{compileStr}");
        
        const string INCLUDE_GROUP = "<ItemGroup Label=\"ClInclude\">";
        var includeGroupIdx = projectFile.IndexOf(INCLUDE_GROUP, StringComparison.Ordinal) + INCLUDE_GROUP.Length;
        projectFile = projectFile.Insert(includeGroupIdx, $"\n{includeStr}");
        
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
            _engineSettingsProvider.GetEngineSourceIncludes());
		
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