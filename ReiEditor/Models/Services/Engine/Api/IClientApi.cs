using System;

namespace ReiEditor.Models.Services.Engine.Api;

public interface IClientApi
{
	void SetDllPtr(IntPtr ptr);

	public IntPtr CreateEngine();
	public void Start(IntPtr enginePtr);
	public int Shutdown(IntPtr enginePtr, int exitCode);
	
	delegate void CallbackDelegate(IntPtr ptr);
	void AddLogCallback(IntPtr ptr);
}