using System;

namespace ReiEditor.Models.Services.Engine.Api;

public interface IEngineApi
{
	void SetDllPtr(IntPtr ptr);

	public IntPtr CreateEngine();
	public void Start(IntPtr enginePtr);
	public int Shutdown(IntPtr enginePtr, int exitCode);
	
	delegate void CallbackDelegate(IntPtr ptr);
	void AddLogCallback(IntPtr ptr);

	long BuildAsset(string assetPath, string destinationFile, long offset);
}