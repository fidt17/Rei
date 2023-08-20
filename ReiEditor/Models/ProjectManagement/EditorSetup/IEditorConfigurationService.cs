using System;
using ReiEditor.Startup.Common;

namespace ReiEditor.Models.ProjectManagement.EditorSetup;

public interface IEditorConfigurationService : IAsyncInitializable
{
	event Action<bool> EditorConfigurationChangedEvent;
	event Action ConfigurationSetEvent;
	
	bool IsEditorConfigurationValid();
	void SaveConfiguration();

	bool IsEngineLocationValid();
	bool SetEngineLocation(string path);
}