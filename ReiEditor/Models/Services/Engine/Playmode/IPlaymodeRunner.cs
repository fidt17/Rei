using System;

namespace ReiEditor.Models.Services.Engine.Playmode;

public interface IPlaymodeRunner
{
    event Action PlaymodeFailedEvent;
    event Action PlaymodeExitedEvent;
	
    void StartPlaymode();
    void StopPlaymode();
}