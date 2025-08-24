using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Engine.Playmode;

public interface IEngineRunner
{
    Utils.Common.IObservable<bool> IsActive { get; }
    Utils.Common.IObservable<bool> IsPlaymodeActive { get; }
    Utils.Common.IObservable<bool> IsEditorActive { get; }
    
    EngineRunMode ActiveMode { get; }
	
    bool StartEngine(EngineRunMode mode);
    Task StopEngine();
}