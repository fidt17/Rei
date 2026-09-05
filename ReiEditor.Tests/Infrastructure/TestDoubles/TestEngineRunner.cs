using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

/// <summary>Controls engine state observables while rejecting attempts to start native execution.</summary>
internal sealed class TestEngineRunner : IEngineRunner
{
    private sealed class TestObservable(Observable<bool> source) : ReiEditor.Utils.Common.IObservable<bool>
    {
        private readonly List<Action<bool>> _subscribers = [];
        public bool Value => source.Value;
        public int SubscriberCount => _subscribers.Count;
        public void Subscribe(Action<bool> callback, bool invoke = true)
        {
            _subscribers.Add(callback);
            source.Subscribe(callback, invoke);
        }
        public void Unsubscribe(Action<bool> callback)
        {
            _subscribers.Remove(callback);
            source.Unsubscribe(callback);
        }
    }

    private readonly TestObservable _playmodeState;
    public event Action? EngineStartedEvent;
    public event Action? EngineStartFailedEvent;
    public int EngineStartedSubscriberCount => EngineStartedEvent?.GetInvocationList().Length ?? 0;
    public int PlaymodeSubscriberCount => _playmodeState.SubscriberCount;
    public Observable<bool> Active { get; } = new(false);
    public Observable<bool> EditorActive { get; } = new(false);
    public Observable<bool> PlaymodeActive { get; } = new(false);
    public Observable<bool> Starting { get; } = new(false);
    public Func<EngineRunMode, bool>? OnStart { get; set; }
    public Func<Task>? OnStop { get; set; }
    public ReiEditor.Utils.Common.IObservable<bool> IsActive => Active;
    public ReiEditor.Utils.Common.IObservable<bool> IsEditorActive => EditorActive;
    public ReiEditor.Utils.Common.IObservable<bool> IsPlaymodeActive => _playmodeState;
    public ReiEditor.Utils.Common.IObservable<bool> IsEngineStarting => Starting;
    public EngineRunMode ActiveMode { get; set; }
    public TestEngineRunner() => _playmodeState = new(PlaymodeActive);
    public void PublishStarted() => EngineStartedEvent?.Invoke();
    public void PublishStartFailed() => EngineStartFailedEvent?.Invoke();
    public bool StartEngine(EngineRunMode mode) => (OnStart ?? throw new NotSupportedException())(mode);
    public Task StopEngine() => (OnStop ?? throw new NotSupportedException())();
}
