using ReiEditor.Startup.Common;

namespace ReiEditor.Models.Services.Engine.Settings;

public interface IEngineSettingsProvider : IAsyncInitializable
{
	string GetEnginePath();
	string GetEngineDebugIncludeDir();
	string GetEngineReleaseIncludeDir();
	string GetEngineSourceIncludes();
	string GetEngineResourcesDir();
	string GetEngineBehavioursDir();
}