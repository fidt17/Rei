namespace ReiEditor.Models.Services.Engine.Dll;

public interface IClientDllManager
{
	bool DllLoaded();
	void LoadDll();
	void UnloadDll();
}