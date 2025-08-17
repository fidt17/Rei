using System;
using System.Collections.Generic;

namespace ReiEditor.Utils.Common;

public class Observable<T> : IObservable<T>
{
    private T? _value;
    public T Value
    {
        get => _value!;
        set
        {
            if (Value != null && Value.Equals(value)) return;
            SetAndInvoke(value);
        }
    }

    private readonly List<Action<T>> _subscribers = new();

    public Observable(T? defaultValue)
    {
        _value = defaultValue;
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

    public void SetAndInvoke(T value)
    {
        _value = value;
			
        for (var i = _subscribers.Count - 1; i >= 0; i--)
        {
            _subscribers[i].Invoke(Value);
        }
    }

    public static implicit operator T(Observable<T> obs) => obs.Value;
}