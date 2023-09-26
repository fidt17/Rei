using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Logging.Engine;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Engine.Playmode;

public class PlaymodeRunner : IPlaymodeRunner
{
	public event Action? PlaymodeFailedEvent;

	private IntPtr? _enginePtr;
	
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
		if (_enginePtr != null) throw new Exception("EnginePtr already exists");
		
		_enginePtr = _clientApi.CreateEngine();
		SetupPlaymode();

		Task.Run(() =>
		{
			try
			{
				_clientApi.Start(_enginePtr.Value);
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
		if (_enginePtr == null) throw new Exception("EnginePtr is missing");
		
		_clientApi.Shutdown(_enginePtr.Value, 1);
		_enginePtr = null;
	}

	private void SetupPlaymode()
	{
		_clientLogger.SubscribeToClient();
	}
}