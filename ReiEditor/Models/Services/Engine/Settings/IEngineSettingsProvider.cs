using ReiEditor.Startup.Common;

namespace ReiEditor.Models.Services.Engine.Settings;

public interface IEngineSettingsProvider : IAsyncInitializable
{
	string GetEngineDebugIncludeDir();
	string GetEngineReleaseIncludeDir();
	string GetEngineSourceIncludes();
}