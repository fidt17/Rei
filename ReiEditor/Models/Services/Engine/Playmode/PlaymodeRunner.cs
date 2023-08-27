using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Logging.Engine;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Engine.Playmode;

public class PlaymodeRunner : IPlaymodeRunner
{
	public event Action? PlaymodeFailedEvent;
	
	private readonly IClientApi _clientApi;
	private readonly ILogger<PlaymodeRunner> _logger;
	private readonly IClientLogger _clientLogger;

	public PlaymodeRunner(IClientApi clientApi, ILogger<PlaymodeRunner> logger, IClientLogger clientLogger)
	{
		_clientApi = clientApi;
		_logger = logger;
		_clientLogger = clientLogger;
	}

	public void StartPlaymode()
	{
		_clientApi.CreateApplication();
		SetupPlaymode();

		Task.Run(() =>
		{
			try
			{
				_clientApi.StartApplication();
			}
			catch (Exception e)
			{
				_logger.LogException(e);
				PlaymodeFailedEvent?.Invoke();
			}
		});
	}

	public void StopPlaymode()
	{
		_clientApi.StopApplication(1);
	}

	private void SetupPlaymode()
	{
		_clientLogger.SubscribeToClient();
	}
}