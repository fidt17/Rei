using System;

namespace ReiEditor.Utils.Common.Condition;

public class Condition : ICondition, IDisposable
{
	public IObservable<bool> IsTrue => _isTrue;
	private readonly Observable<bool> _isTrue = new(false);

	private readonly IObservable<bool> _observable;
	private readonly bool _target;

	public Condition(IObservable<bool> observable, bool target = false)
	{
		_target = target;
		_observable = observable;
		_observable.Subscribe(HandleObservableValueChangedEvent);
	}

	public void Dispose()
	{
		_observable.Unsubscribe(HandleObservableValueChangedEvent);
	}

	private void HandleObservableValueChangedEvent(bool value) => _isTrue.Value = value == _target;
}