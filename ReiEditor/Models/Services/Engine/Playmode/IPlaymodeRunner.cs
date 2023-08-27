using System;

namespace ReiEditor.Models.Services.Engine.Playmode;

public interface IPlaymodeRunner
{
	event Action PlaymodeFailedEvent;
	
	void StartPlaymode();
	void StopPlaymode();
}