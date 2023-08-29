using System;
using ReiEditor.Startup.Common;

namespace ReiEditor.Models.ProjectManagement.EditorSetup;

public interface IEditorSettingsService : IAsyncInitializable
{
	event Action<bool> EditorConfigurationChangedEvent;
	event Action ConfigurationSetEvent;
	
	bool IsEditorConfigurationValid();
	void SaveConfiguration();

	bool IsEngineLocationValid();
	bool SetEngineLocation(string path);
	string GetEngineLocation();

	bool IsMsBuildLocationValid();
	bool SetMsBuildLocation(string path);
	string GetMsBuildLocation();
}