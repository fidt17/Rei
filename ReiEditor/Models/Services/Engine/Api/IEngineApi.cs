using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Api.DTO;

namespace ReiEditor.Models.Services.Engine.Api;

public interface IEngineApi
{
	delegate void CallbackDelegate(IntPtr ptr);
	
	void SetDllPtr(IntPtr ptr);

	IntPtr CreateEngine(string resourcesDir);
	void Start(IntPtr enginePtr);
	void Shutdown(IntPtr enginePtr, int exitCode);
	
	void AddLogCallback(IntPtr ptr);
	void AddShutdownCallback(IntPtr callback);

	EntityStateResponse? GetSceneEntityState(int sceneEntityId);
	bool RenameEntity(int sceneEntityId, string newName);
	
	long BuildAsset(string assetPath, string destinationFile, long offset);

	Task<IntPtr> CreatePlaymodeWindow();
	IntPtr GetWindowHandle(IntPtr windowPtr);
	void ResizeWindow(IntPtr windowPtr, int width, int height);
}