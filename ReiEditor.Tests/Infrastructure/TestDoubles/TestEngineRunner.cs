using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

/// <summary>Controls engine state observables while rejecting attempts to start native execution.</summary>
internal sealed class TestEngineRunner : IEngineRunner
{
    public event Action EngineStartedEvent { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
    public event Action EngineStartFailedEvent { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
    public Observable<bool> Active { get; } = new(false);
    public Observable<bool> EditorActive { get; } = new(false);
    public Observable<bool> PlaymodeActive { get; } = new(false);
    public ReiEditor.Utils.Common.IObservable<bool> IsActive => Active;
    public ReiEditor.Utils.Common.IObservable<bool> IsEditorActive => EditorActive;
    public ReiEditor.Utils.Common.IObservable<bool> IsPlaymodeActive => PlaymodeActive;
    public ReiEditor.Utils.Common.IObservable<bool> IsEngineStarting { get; } = new Observable<bool>(false);
    public EngineRunMode ActiveMode { get; set; }
    public bool StartEngine(EngineRunMode mode) => throw new NotSupportedException();
    public Task StopEngine() => throw new NotSupportedException();
}
