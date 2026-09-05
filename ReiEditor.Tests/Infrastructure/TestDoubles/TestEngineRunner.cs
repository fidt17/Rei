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
    public Observable<bool> Starting { get; } = new(false);
    public Func<EngineRunMode, bool>? OnStart { get; set; }
    public Func<Task>? OnStop { get; set; }
    public ReiEditor.Utils.Common.IObservable<bool> IsActive => Active;
    public ReiEditor.Utils.Common.IObservable<bool> IsEditorActive => EditorActive;
    public ReiEditor.Utils.Common.IObservable<bool> IsPlaymodeActive => PlaymodeActive;
    public ReiEditor.Utils.Common.IObservable<bool> IsEngineStarting => Starting;
    public EngineRunMode ActiveMode { get; set; }
    public bool StartEngine(EngineRunMode mode) => (OnStart ?? throw new NotSupportedException())(mode);
    public Task StopEngine() => (OnStop ?? throw new NotSupportedException())();
}
