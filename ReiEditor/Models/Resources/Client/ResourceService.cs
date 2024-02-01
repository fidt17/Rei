using System;
using System.Collections.Generic;
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

	public string GetRootPath() => _resourcesPath;

	public string GetProjectPath(params string[] path)
	{
		return Path.GetFullPath(Path.Combine(_resourcesPath, "Project", Path.Combine(path)));
	}

	public string GetSolutionPath(params string[] path)
	{
		return Path.GetFullPath(Path.Combine(GetProjectPath("Scripts"), Path.Combine(path)));
	}

	public IEnumerable<string> GetAllWithExtension(string extension)
	{
		return Directory.EnumerateFiles(GetRootPath(), $"*{extension}", SearchOption.AllDirectories);
	}

	public async Task<T?> Load<T>(string fullPath)
	{
		try
		{
			var data = await ResourceUtils.Load(fullPath);
			return _serializer.Deserialize<T>(data);
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}

		return default;
	}

	public async Task<string?> Load(string fullPath)
	{
		try
		{
			return await ResourceUtils.Load(fullPath);
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}

		return null;
	}

	public async Task<bool> Write(string data, string fullPath)
	{
		try
		{
			Directory.CreateDirectory(fullPath.Replace(Path.GetFileName(fullPath), ""));
			await File.WriteAllTextAsync(fullPath, data);
			return true;
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}

		return false;
	}

	public bool Exists(string fullPath)
	{
		return File.Exists(fullPath);
	}
}