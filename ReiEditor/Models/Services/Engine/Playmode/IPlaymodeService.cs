using System;

namespace ReiEditor.Models.Services.Engine.Playmode;

public interface IPlaymodeService
{
	event Action<bool> PlaymodeActiveValueChangedEvent;
	
	bool PlaymodeActive { get; }

	bool CanStartPlaymode();
	bool CanStopPlaymode();

	void StartPlaymode();
	void StopPlaymode();
}