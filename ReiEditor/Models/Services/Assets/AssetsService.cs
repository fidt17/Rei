using System;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Serialization;

namespace ReiEditor.Models.Services.Assets;

public class AssetsService : IAssetsService
{
	private readonly ILogger<AssetsService> _logger;
	private readonly IResourceService _resourceService;
	private readonly ISerializer _serializer;
	private readonly IActiveProjectService _activeProject;

	public AssetsService(ILogger<AssetsService> logger, IResourceService resourceService, ISerializer serializer, IActiveProjectService activeProject)
	{
		_logger = logger;
		_resourceService = resourceService;
		_serializer = serializer;
		_activeProject = activeProject;
	}

	public string AllocateAssetId() => new(Guid.NewGuid().ToString());

	public Task<bool> Create(Asset asset, string projectPath)
	{
		try
		{
			var fullPath = _resourceService.GetFullPath(projectPath, $"{asset.Name}{AssetUtils.AssetFileExtensions[asset.GetType()]}");
			if (_resourceService.Exists(fullPath)) throw new Exception($"Cannot create asset because another file exists at {fullPath}");
			
			var data = _serializer.Serialize(asset);
			
			Directory.CreateDirectory(_resourceService.GetFullPath(projectPath));
			return _resourceService.Write(data, fullPath);
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}
		
		return Task.FromResult(false);
	}

	public async Task SaveProject()
	{
		var project = _activeProject.GetActiveProject();
		project.SetProjectLastEditTime(DateTime.Now);
		await File.WriteAllTextAsync(project.ProjectFilePath, _serializer.Serialize(project));
		
		// todo: save dirty assets
	}
}