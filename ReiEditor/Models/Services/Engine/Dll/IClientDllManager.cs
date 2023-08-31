using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Engine.Dll;

public interface IClientDllManager
{
	Observable<bool> DllLoaded { get; }
	void LoadDll();
	void UnloadDll();
}