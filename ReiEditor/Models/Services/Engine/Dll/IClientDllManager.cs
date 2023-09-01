using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Engine.Dll;

public interface IClientDllManager
{
	IObservable<bool> DllLoaded { get; }
	void LoadDll();
	void UnloadDll();
}