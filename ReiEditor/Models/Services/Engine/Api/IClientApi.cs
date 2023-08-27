using System;

namespace ReiEditor.Models.Services.Engine.Api;

public interface IClientApi
{
	void SetDllPtr(IntPtr ptr);

	void CreateApplication();
	void StartApplication();
	int StopApplication(int code);
	
	delegate void CallbackDelegate(IntPtr ptr);
	void AddLogCallback(IntPtr ptr);
}