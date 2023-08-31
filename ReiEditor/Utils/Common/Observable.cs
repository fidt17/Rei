using System;
using System.Collections.Generic;

namespace ReiEditor.Utils.Common;

public class Observable<T>
{
	private T? _value;
	public T Value
	{
		get => _value ?? throw new NullReferenceException();
		set
		{
			if (Value != null && Value.Equals(value)) return;
			_value = value;
			
			for (var i = _subscribers.Count - 1; i >= 0; i--)
			{
				_subscribers[i].Invoke(Value);
			}
		}
	}

	private readonly List<Action<T>> _subscribers = new();

	public Observable(T defaultValue)
	{
		Value = defaultValue;
	}

	public void Subscribe(Action<T> callback, bool invoke = true)
	{
		_subscribers.Add(callback);
		if (invoke)
		{
			callback.Invoke(Value);
		}
	}

	public void Unsubscribe(Action<T> callback)
	{
		_subscribers.Remove(callback);
	}

	public static implicit operator T(Observable<T> obs) => obs.Value;
}