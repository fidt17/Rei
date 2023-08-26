using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Engine.Api;

public class ClientApi : IClientApi
{
	public const string CLIENT_DLL = "Client.dll";
	
	private IntPtr _dllPtr;
	private readonly ILogger<ClientApi> _logger;

	public ClientApi(ILogger<ClientApi> logger)
	{
		_logger = logger;
	}

	public void SetDllPtr(IntPtr dllPtr)
	{
		if (dllPtr == IntPtr.Zero) throw new Exception("Invalid dll");
		_dllPtr = dllPtr;
	}

	public void CreateApplication() => Invoke();
	public void StartApplication() => Invoke();
	
	private delegate int ShutdownApplicationDelegate(int code);
	public int StopApplication(int code) => Invoke<int>(typeof(ShutdownApplicationDelegate), nameof(StopApplication), code);

	private delegate void callbackDelegate(IntPtr callback);
	public void AddLogCallback(IntPtr callback) => Invoke(typeof(callbackDelegate), nameof(AddLogCallback), callback);

	#region UTILS
	
	[DllImport("Kernel32.dll")]
	private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

	private void Invoke([CallerMemberName] string caller = "")
	{
		try
		{
			Marshal.GetDelegateForFunctionPointer(GetProcAddress(_dllPtr, caller), typeof(Action)).DynamicInvoke();
		}
		catch (Exception)
		{
			_logger.LogError($"Dll invoke error [{caller}]");
			throw;
		}
	}
	
	private void Invoke(Type delegateType, [CallerMemberName] string caller = "", params object?[]? args)
	{
		try
		{
			var d = Marshal.GetDelegateForFunctionPointer(GetProcAddress(_dllPtr, caller), delegateType);
			d.DynamicInvoke(args);
		}
		catch (Exception)
		{
			_logger.LogError($"Dll invoke error [{caller}]");
			throw;
		}
	}

	private T Invoke<T>(Type delegateType, [CallerMemberName] string caller = "", params object?[]? args)
	{
		try
		{
			return (T?) Marshal.GetDelegateForFunctionPointer(GetProcAddress(_dllPtr, caller), delegateType).DynamicInvoke(args) ?? throw new InvalidOperationException(caller);
		}
		catch (Exception)
		{
			_logger.LogError($"Dll invoke error [{caller}]");
			throw;
		}
	}

	#endregion
}