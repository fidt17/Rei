using System;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Factory;

namespace ReiEditor.Models.Services.Engine.Playmode;

public class PlaymodeService : IPlaymodeService, IDisposable
{
	public event Action<bool>? PlaymodeActiveValueChangedEvent;

	private bool _playmodeActive;
	public bool PlaymodeActive
	{
		get => _playmodeActive;
		private set
		{
			if (value == PlaymodeActive) return;
			_playmodeActive = value;
			PlaymodeActiveValueChangedEvent?.Invoke(value);
		}
	}

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
		if (PlaymodeActive)
		{
			StopPlaymode();
		}

		if (_clientDllManager.DllLoaded())
		{
			_clientDllManager.UnloadDll();
		}
	}

	public bool CanStartPlaymode() => !PlaymodeActive;
	public bool CanStopPlaymode() => PlaymodeActive;

	public void StartPlaymode()
	{
		if (!CanStartPlaymode())
		{
			_logger.LogError("Cannot start playmode");
			return;
		}

		_logger.Log("Start Playmode");
		_activePlaymodeRunner = _playmodeRunnerFactory.CreateInstance();
		_activePlaymodeRunner.PlaymodeFailedEvent += StopPlaymode;
			
		try
		{
			if (_clientDllManager.DllLoaded())
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
		
		PlaymodeActive = true;
	}

	public void StopPlaymode()
	{
		if (!CanStopPlaymode())
		{
			_logger.LogError("Cannot stop playmode");
			return;
		}

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
		
		PlaymodeActive = false;
	}
}