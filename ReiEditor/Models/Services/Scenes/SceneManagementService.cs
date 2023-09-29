using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Scenes;

public class SceneManagementService : ISceneManagementService
{
	private readonly ILogger<SceneManagementService> _logger;
	private readonly IAssetsService _assets;

	public SceneManagementService(ILogger<SceneManagementService> logger, IAssetsService assets)
	{
		_logger = logger;
		_assets = assets;
	}

	public async Task<Scene?> CreateScene(string name, string projectPath)
	{
		try
		{
			var scene = new Scene(_assets.AllocateAssetId(), name);
			var didCreate = await _assets.Create(scene, projectPath);
			if (!didCreate) throw new Exception("Scene creation failed");

			return scene;
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}

		return null;
	}
}