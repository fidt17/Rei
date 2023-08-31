using System;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Factory;

namespace ReiEditor.Models.Services.Engine.Playmode;

public class PlaymodeService : IPlaymodeService, IDisposable
{
	public Observable<bool> IsPlaymodeActive { get; } = new Observable<bool>(false);

	private IPlaymodeRunner? _activePlaymodeRunner;

	private readonly ILogger<PlaymodeService> _logger;
	private readonly IClientDllManager _clientDllManager;
	private readonly IFactory<IPlaymodeRunner> _playmodeRunnerFactory;

	public PlaymodeService(ILogger<PlaymodeService> logger, IClientDllManager clientDllManager, IFactory<IPlaymodeRunner> playmodeRunnerFactory)
	{
		_logger = logger;
		_clientDllManager = clientDllManager;
		_playmodeRunnerFactory = playmodeRunnerFactory;
	}
	
	public void Dispose()
	{
		if (IsPlaymodeActive)
		{
			StopPlaymode();
		}
	}

	public void StartPlaymode()
	{
		if (IsPlaymodeActive)
		{
			_logger.LogError("Playmode is already active");
			return;
		}

		_logger.Log("Start Playmode");
		_activePlaymodeRunner = _playmodeRunnerFactory.CreateInstance();
		_activePlaymodeRunner.PlaymodeFailedEvent += StopPlaymode;
			
		try
		{
			if (_clientDllManager.DllLoaded)
			{
				_clientDllManager.UnloadDll();
			}
			
			_clientDllManager.LoadDll();
		}
		catch (Exception e)
		{
			_logger.LogError("Could not load client dll");
			_logger.LogException(e);
			return;
		}

		try
		{
			_activePlaymodeRunner.StartPlaymode();
		}
		catch (Exception e)
		{
			_logger.LogError("Could not start Playmode");
			_logger.LogException(e);
			return;
		}

		IsPlaymodeActive.Value = true;
	}

	public void StopPlaymode()
	{
		if (!IsPlaymodeActive) return;

		_logger.Log("Stop Playmode");

		try
		{
			_activePlaymodeRunner?.StopPlaymode();
		}
		catch (Exception e)
		{
			_logger.LogError("Could not stop Playmode");
			_logger.LogException(e);
		}
			
		try
		{
			_clientDllManager.UnloadDll();
		}
		catch (Exception e)
		{
			_logger.LogError("Could not unload client dll");
			_logger.LogException(e);
		}

		IsPlaymodeActive.Value = false;
	}
}