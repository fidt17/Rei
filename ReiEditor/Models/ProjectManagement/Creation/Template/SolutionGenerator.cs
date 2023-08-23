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
		var projectDirPath = Path.Combine(solutionDirPath, "Scripts");
			
		_logger.Log($"Creating solution directory at: {solutionDirPath}");
		Directory.CreateDirectory(solutionDirPath);

		_logger.Log($"Creating project directory at: {projectDirPath}");
		Directory.CreateDirectory(projectDirPath);

		var solutionGuid = Guid.NewGuid();
		var projectGuid = Guid.NewGuid();
			
		var solutionFilePath = await CreateSolutionFile(config.ProjectName, solutionDirPath, solutionGuid, projectGuid);
		await CreateProjectFile(config.ProjectName, projectDirPath, projectGuid);

		return solutionFilePath;
	}

	private async Task<string> CreateSolutionFile(string projectName, string solutionFolderPath, Guid solutionGuid, Guid projectGuid)
	{
		_logger.Log("Creating solution file");
		
		var solutionTemplate = await _templateProvider.GetVSSolutionTemplate();
		var filledTemplate = string.Format(solutionTemplate, projectName, FormatGuid(projectGuid), FormatGuid(solutionGuid));
		
		var filePath = Path.Combine(solutionFolderPath, $"{projectName}{FileExtensions.VS_SOLUTION}");
		
		await File.WriteAllTextAsync(filePath, filledTemplate);

		return filePath;
	}

	private async Task CreateProjectFile(string projectName, string projectFolderPath, Guid projectGuid)
	{
		_logger.Log("Creating VS project file");
		
		var projectTemplate = await _templateProvider.GetVSProjectTemplate();
		var filledTemplate = string.Format(projectTemplate, FormatGuid(projectGuid), projectName, 
			_engineSettingsProvider.GetEngineDebugIncludeDir(), _engineSettingsProvider.GetEngineReleaseIncludeDir(), _engineSettingsProvider.GetEngineSourceIncludeDir());
		
		var filePath = Path.Combine(projectFolderPath, $"{projectName}{FileExtensions.VS_PROJECT}");
		
		await File.WriteAllTextAsync(filePath, filledTemplate);
	}

	private static string FormatGuid(Guid guid) => "{" + guid + "}";
}