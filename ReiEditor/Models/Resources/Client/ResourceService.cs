using System;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Resources.Client;

public class ResourceService : IResourceService
{
	private readonly ILogger<ResourceService> _logger;
	private readonly IActiveProjectService _activeProjectService;
	private readonly string _resourcesPath;

	public ResourceService(ILogger<ResourceService> logger, IActiveProjectService activeProjectService)
	{
		_logger = logger;
		_activeProjectService = activeProjectService;
		_resourcesPath = _activeProjectService.GetActiveProject().GetDirectoryPath();
	}

	public string GetFullPath(params string[] path)
	{
		return Path.Combine(_resourcesPath, Path.Combine(path));
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

	public bool Copy(string from, string to, bool overrideContents)
	{
		try
		{
			File.Copy(from, to, overrideContents);
			return true;
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}

		return false;
	}
}