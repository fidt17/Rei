using System;

namespace ReiEditor.Utils.Common.Condition;

public interface ICondition : IDisposable
{
	Observable<bool> IsTrue { get; }
}