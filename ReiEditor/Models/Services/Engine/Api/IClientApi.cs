using System;

namespace ReiEditor.Models.Services.Engine.Api;

public interface IClientApi
{
	void SetDllPtr(IntPtr ptr);
	void StartApplication();
	
	delegate void CallbackDelegate(string str);
	void AddLog(IntPtr ptr);
	
	int ShutdownApplication(int code);
}