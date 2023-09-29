using System;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.ProjectManagement.Creation.Template;

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

	public async Task<string> GenerateSolution(ProjectCreationConfiguration config)
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
		await CreateProjectFile(config.ProjectName, projectDirPath, projectGuid);
		await CreateSourceFiles(projectDirPath);

		return solutionFilePath;
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

	private async Task CreateProjectFile(string projectName, string projectFolderPath, Guid projectGuid)
	{
		_logger.Log("Creating VS project file");
		
		var projectTemplate = await _templateProvider.GetVSProjectTemplate();
		var filledTemplate = string.Format(
			projectTemplate, 
			FormatGuid(projectGuid), projectName, 
			_engineSettingsProvider.GetEngineDebugIncludeDir(), 
			_engineSettingsProvider.GetEngineReleaseIncludeDir(), 
			_engineSettingsProvider.GetEngineSourceIncludeDir(),
			GetMainFileName());
		
		var filePath = Path.Combine(projectFolderPath, $"{projectName}{FileExtensions.VS_PROJECT}");
		
		await File.WriteAllTextAsync(filePath, filledTemplate);
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
}