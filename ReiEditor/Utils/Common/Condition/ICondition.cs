using System;

namespace ReiEditor.Utils.Common.Condition;

public interface ICondition : IDisposable
{
	IObservable<bool> IsTrue { get; }
}