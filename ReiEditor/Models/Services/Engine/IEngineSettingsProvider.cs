using ReiEditor.Startup.Common;

namespace ReiEditor.Models.Services.Engine;

public interface IEngineSettingsProvider : IAsyncInitializable
{
	string GetEngineDebugIncludeDir();
	string GetEngineReleaseIncludeDir();
	string GetEngineSourceIncludeDir();
}