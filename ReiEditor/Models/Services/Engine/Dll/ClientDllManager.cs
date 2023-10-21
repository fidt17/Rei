using System;
using System.IO;
using System.Runtime.InteropServices;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Engine.Dll;

public class ClientDllManager : IClientDllManager, IDisposable
{
	public Utils.Common.IObservable<bool> DllLoaded => _dllLoaded;
	
	private IntPtr _loadedDllPtr;

	private readonly Observable<bool> _dllLoaded = new(false);

	private readonly ILogger<ClientDllManager> _logger;
	private readonly IActiveProjectService _activeProjectService;
	private readonly IEngineApi _engineApi;

	public ClientDllManager(ILogger<ClientDllManager> logger, IActiveProjectService activeProjectService, IEngineApi engineApi)
	{
		_logger = logger;
		_activeProjectService = activeProjectService;
		_engineApi = engineApi;
	}

	public void Dispose()
	{
		if (_dllLoaded)
		{
			UnloadDll();
		}
	}

	public void LoadDll()
	{
		if (_dllLoaded)
		{
			_logger.LogError("Dll is already loaded");
			return;
		}
			
		var dllPath = GetDllPath();

		SetDllDirectory(Path.GetDirectoryName(dllPath)!);
		_loadedDllPtr = LoadLibrary(GetProjectDllName());
		_engineApi.SetDllPtr(_loadedDllPtr);
		_dllLoaded.Value = true;
		_logger.Log($"Loaded client dll");
	}

	public void UnloadDll()
	{
		if (!_dllLoaded) return;

		FreeLibrary(_loadedDllPtr);

		_dllLoaded.Value = false;
		_loadedDllPtr = IntPtr.Zero;
		_logger.Log($"Unloaded client dll");
	}
	
	private string GetProjectDllName() => $"{_activeProjectService.GetActiveProject().ProjectName}.dll";

	private string GetDllPath()
	{
		var project = _activeProjectService.GetActiveProject();
		var root = project.GetDirectoryPath();
		var buildDllPath = Path.Combine(root, "bin", "x64EditorDebug", project.ProjectName, $"{project.ProjectName}.dll");
		return buildDllPath;
	}
	
	[DllImport("kernel32.dll")]
	private static extern bool SetDllDirectory(string lpPathName);
	
	[DllImport("kernel32.dll")]
	private static extern IntPtr LoadLibrary(string dllToLoad);
	
	[DllImport("kernel32.dll", SetLastError=true)]
	private static extern bool FreeLibrary(IntPtr hModule);
}