using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Engine.Playmode;

public class PlaymodeService : IPlaymodeService, IAsyncDisposable
{
	public event Action<bool>? PlaymodeActiveValueChangedEvent;
	
	public bool PlaymodeActive { get; private set; }

	private bool _isStoppingPlaymode;
	
	private readonly ILogger<PlaymodeService> _logger;
	private readonly IClientDllManager _clientDllManager;
	private readonly IClientApi _clientApi;

	public PlaymodeService(ILogger<PlaymodeService> logger, IClientDllManager clientDllManager, IClientApi clientApi)
	{
		_logger = logger;
		_clientDllManager = clientDllManager;
		_clientApi = clientApi;
	}
	
	public ValueTask DisposeAsync()
	{
		if (PlaymodeActive)
		{
			StopPlaymode();
		}
		_clientDllManager.UnloadDll();
		return ValueTask.CompletedTask;
	}

	public bool CanStartPlaymode() => !PlaymodeActive;
	public bool CanStopPlaymode() => PlaymodeActive && !_isStoppingPlaymode;

	private IClientApi.CallbackDelegate _logDelegate;
	public void StartPlaymode()
	{
		
		if (!CanStartPlaymode())
		{
			_logger.LogError("Cannot start playmode");
			return;
		}

		try
		{
			_logger.Log("Start play mode");
			_clientDllManager.LoadDll();
			Task.Run(StartApplicationTask);
		}
		catch (Exception e)
		{
			_logger.LogException(e);
			_logger.LogError("Could not start playmode");
			return;
		}
		
		PlaymodeActive = true;
		InvokePlaymodeActiveValueChangedEvent();
	}

	public Task StopPlaymode()
	{
		if (!CanStopPlaymode()) throw new Exception("Cannot stop playmode");

		try
		{
			_isStoppingPlaymode = true;
			_logger.Log("Stop play mode");
			var exitCode = _clientApi.StopApplication(666);
			_logger.Log($"Exit code: {exitCode}");
			_clientDllManager.UnloadDll();
			
			PlaymodeActive = false;
			InvokePlaymodeActiveValueChangedEvent();
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}

		_isStoppingPlaymode = false;
		return Task.CompletedTask;
	}

	private Task StartApplicationTask()
	{
		try
		{
			_clientApi.CreateApplication();
					
			void callback(string str) => _logger.Log(str);
			_logDelegate = new IClientApi.CallbackDelegate(callback);
			_clientApi.AddLogCallback(Marshal.GetFunctionPointerForDelegate(_logDelegate));
					
			_clientApi.StartApplication();
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}

		return Task.CompletedTask;
	}

	private void InvokePlaymodeActiveValueChangedEvent() => PlaymodeActiveValueChangedEvent?.Invoke(PlaymodeActive);
}