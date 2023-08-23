using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ReiEditor.Models.Services.Engine.Api;

public class ClientApi : IClientApi
{
	public const string CLIENT_DLL = "Client.dll";
	
	[DllImport("Kernel32.dll")]
	private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
	
	private IntPtr _dllPtr;

	public void SetDllPtr(IntPtr dllPtr)
	{
		if (dllPtr == IntPtr.Zero) throw new Exception("Invalid dll");
		_dllPtr = dllPtr;
	}

	public void StartApplication() => Invoke();

	private delegate void callbackDelegate(IntPtr callback);
	public void AddLog(IntPtr callback) => Invoke(typeof(callbackDelegate), nameof(AddLog), callback);

	private delegate int ShutdownApplicationDelegate(int code);


	public int ShutdownApplication(int code) => Invoke<int>(typeof(ShutdownApplicationDelegate), nameof(ShutdownApplication), code);

	private void Invoke([CallerMemberName] string caller = "")
	{
		Marshal.GetDelegateForFunctionPointer(GetProcAddress(_dllPtr, caller), typeof(Action)).DynamicInvoke();
	}
	
	private void Invoke(Type delegateType, [CallerMemberName] string caller = "", params object?[]? args)
	{
		var d = Marshal.GetDelegateForFunctionPointer(GetProcAddress(_dllPtr, caller), delegateType);
		d.DynamicInvoke(args);
	}

	private T Invoke<T>(Type delegateType, [CallerMemberName] string caller = "", params object?[]? args)
	{
		return (T?) Marshal.GetDelegateForFunctionPointer(GetProcAddress(_dllPtr, caller), delegateType).DynamicInvoke(args) ?? throw new InvalidOperationException(caller);
	}
}