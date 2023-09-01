using System;
using System.IO;
using System.Runtime.InteropServices;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common;
using Exception = System.Exception;

namespace ReiEditor.Models.Services.Engine.Dll;

public class ClientDllManager : IClientDllManager, IDisposable
{
	public Utils.Common.IObservable<bool> DllLoaded => _dllLoaded;
	
	private IntPtr _loadedDllPtr;

	private readonly Observable<bool> _dllLoaded = new();

	private readonly ILogger<ClientDllManager> _logger;
	private readonly IResourceService _resourceService;
	private readonly IActiveProjectService _activeProjectService;
	private readonly IClientApi _clientApi;

	public ClientDllManager(ILogger<ClientDllManager> logger, IResourceService resourceService, IActiveProjectService activeProjectService, IClientApi clientApi)
	{
		_logger = logger;
		_resourceService = resourceService;
		_activeProjectService = activeProjectService;
		_clientApi = clientApi;
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
		_loadedDllPtr = LoadLibrary(ClientApi.CLIENT_DLL);
		_clientApi.SetDllPtr(_loadedDllPtr);
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

	private string GetDllPath()
	{
		var project = _activeProjectService.GetActiveProject();
		var buildDllPath = _resourceService.GetFullPath("bin", "x64EditorDebug", project.ProjectName, $"{project.ProjectName}.dll");
		
		if (!File.Exists(buildDllPath))
		{ 
			// in case of empty project -> use engine dll directly
			buildDllPath = _resourceService.GetFullPath("bin", "x64EditorDebug", project.ProjectName, $"Rei.dll");
		}
		
		var copyDllPath = _resourceService.GetFullPath("bin", "x64EditorDebug", project.ProjectName, $"{ClientApi.CLIENT_DLL}");
		if (!_resourceService.Copy(buildDllPath, copyDllPath, overrideContents: true)) throw new Exception("Could not copy client dll");

		return copyDllPath;
	}
	
	[DllImport("kernel32.dll")]
	private static extern bool SetDllDirectory(string lpPathName);
	
	[DllImport("kernel32.dll")]
	private static extern IntPtr LoadLibrary(string dllToLoad);
	
	[DllImport("kernel32.dll", SetLastError=true)]
	private static extern bool FreeLibrary(IntPtr hModule);
}