using System;

namespace ReiEditor.Utils.Common.Condition;

public class Condition : ICondition, IDisposable
{
	public Observable<bool> IsTrue { get; }

	private readonly Observable<bool> _observable;
	private readonly bool _target;

	public Condition(Observable<bool> observable, bool target = false)
	{
		IsTrue = new Observable<bool>(false);
		_target = target;
		_observable = observable;
		_observable.Subscribe(HandleObservableValueChangedEvent);
	}

	public void Dispose()
	{
		_observable.Unsubscribe(HandleObservableValueChangedEvent);
	}

	private void HandleObservableValueChangedEvent(bool value) => IsTrue.Value = value == _target;
}