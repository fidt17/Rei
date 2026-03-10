using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Engine.Dll;

public interface IClientDllManager
{
	bool DllExists(string? dllPath = null);
	IObservable<bool> DllLoaded { get; }
	void LoadDll(string? dllPath = null);
	bool UnloadDll();
}
