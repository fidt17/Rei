using System;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Serialization;

namespace ReiEditor.Models.Resources.Client;

public class ResourceService : IResourceService
{
	private readonly ILogger<ResourceService> _logger;
	private readonly IActiveProjectService _activeProjectService;
	private readonly ISerializer _serializer;
	private readonly string _resourcesPath;

	public ResourceService(ILogger<ResourceService> logger, IActiveProjectService activeProjectService, ISerializer serializer)
	{
		_logger = logger;
		_activeProjectService = activeProjectService;
		_serializer = serializer;
		_resourcesPath = _activeProjectService.GetActiveProject().GetDirectoryPath();
	}

	public string GetFullPath(params string[] path)
	{
		return Path.GetFullPath(Path.Combine(_resourcesPath, "Project", Path.Combine(path)));
	}

	public string GetSolutionPath(params string[] path)
	{
		return Path.GetFullPath(Path.Combine(GetFullPath("Scripts"), Path.Combine(path)));
	}

	public async Task<T?> Load<T>(string path)
	{
		try
		{
			var data = await ResourceUtils.Load(path);
			return _serializer.Deserialize<T>(data);
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}

		return default;
	}

	public async Task<string?> Load(string path)
	{
		try
		{
			return await ResourceUtils.Load(path);
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}

		return null;
	}

	public async Task<bool> Write(string data, string path)
	{
		try
		{
			Directory.CreateDirectory(path.Replace(Path.GetFileName(path), ""));
			await File.WriteAllTextAsync(path, data);
			return true;
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}

		return false;
	}

	public bool Exists(string path)
	{
		return File.Exists(path);
	}
}