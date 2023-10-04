using System;

namespace ReiEditor.Utils.Common;

public interface IObservable<T>
{
	T Value { get; }
	void Subscribe(Action<T> callback, bool invoke = true);
	void Unsubscribe(Action<T> callback);
}