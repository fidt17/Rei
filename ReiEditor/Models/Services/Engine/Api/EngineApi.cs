using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Engine.Api;

public class EngineApi : IEngineApi
{
	private IntPtr _dllPtr;
	private readonly ILogger<EngineApi> _logger;

	public EngineApi(ILogger<EngineApi> logger)
	{
		_logger = logger;
	}

	public void SetDllPtr(IntPtr dllPtr)
	{
		if (dllPtr == IntPtr.Zero) throw new Exception("Invalid dll");
		_dllPtr = dllPtr;
	}

	private delegate IntPtr CreateEngineDelegate();
	public IntPtr CreateEngine() => Invoke<IntPtr>(typeof(CreateEngineDelegate));

	private delegate void StartEngineDelegate(IntPtr enginePtr);
	public void Start(IntPtr enginePtr) => Invoke(typeof(StartEngineDelegate), "Start", enginePtr);
	
	private delegate int ShutdownEngineDelegate(IntPtr enginePtr, int exitCode);
	public int Shutdown(IntPtr enginePtr, int exitCode) => Invoke<int>(typeof(ShutdownEngineDelegate), "Shutdown", enginePtr, exitCode);

	private delegate void callbackDelegate(IntPtr callback);
	public void AddLogCallback(IntPtr callback) => Invoke(typeof(callbackDelegate), nameof(AddLogCallback), callback);

	private delegate long BuildAssetDelegate(string path, string dest, long offset);
	public long BuildAsset(string assetPath, string destinationFile, long offset) => Invoke<long>(typeof(BuildAssetDelegate), "BuildAsset", assetPath, destinationFile, offset);

	#region UTILS
	
	[DllImport("Kernel32.dll")]
	private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

/*
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
*/
	
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
		catch (Exception e)
		{
			_logger.LogError($"Dll invoke error [{caller}]");
			throw;
		}
	}

	#endregion
}