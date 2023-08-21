using System;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Resources;

public class ResourceLoader : IResourceLoader
{
	private readonly ILogger<ResourceLoader> _logger;
	private readonly string _resourcesPath;

	public ResourceLoader(ILogger<ResourceLoader> logger)
	{
		_logger = logger;
		_resourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
	}

	public async Task<string?> Load(params string[] path)
	{
		try
		{
			var resourcePath = Path.Combine(_resourcesPath, Path.Combine(path));
			if (!File.Exists(resourcePath)) throw new Exception($"Resource does not exist at path: {resourcePath}");

			var result = await File.ReadAllTextAsync(resourcePath);
			return result;
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}

		return null;
	}
}