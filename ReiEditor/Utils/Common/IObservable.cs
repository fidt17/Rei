using System;

namespace ReiEditor.Utils.Common;

public interface IObservable<T>
{
	public T Value { get; }
	void Subscribe(Action<T> callback, bool invoke = true);
	public void Unsubscribe(Action<T> callback);
}