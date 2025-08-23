using System;

namespace ReiEditor.Models.Services.Engine.Playmode;

public interface IEngineRunner
{
    event Action EngineShutdownEvent;

    Utils.Common.IObservable<bool> IsActive { get; }
    Utils.Common.IObservable<bool> IsPlaymodeActive { get; }
    
    EngineRunMode ActiveMode { get; }
	
    bool StartEngine(EngineRunMode mode);
    void StopEngine();
}