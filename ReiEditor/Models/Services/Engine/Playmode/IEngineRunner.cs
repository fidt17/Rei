using System;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Engine.Playmode;

public interface IEngineRunner
{
    event Action EngineStartedEvent;
    
    Utils.Common.IObservable<bool> IsActive { get; }
    Utils.Common.IObservable<bool> IsPlaymodeActive { get; }
    Utils.Common.IObservable<bool> IsEditorActive { get; }
    Utils.Common.IObservable<bool> IsEngineStarting { get; }
    
    EngineRunMode ActiveMode { get; }
	
    bool StartEngine(EngineRunMode mode);
    Task StopEngine();
}