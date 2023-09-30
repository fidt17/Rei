using System;
using System.Collections.Generic;
using System.Linq;

namespace ReiEditor.Utils.Common.Condition;

public class ConditionGroup : ICondition, IDisposable
{
	public IObservable<bool> IsTrue => _isTrue;

	private readonly Observable<bool> _isTrue = new(false);

	private readonly List<ICondition> _conditions;

	public ConditionGroup(params ICondition[] conditions)
	{
		_conditions = conditions.ToList();
		_conditions.ForEach(x => x.IsTrue.Subscribe(HandleConditionValueChangedEvent, invoke: false));
		UpdateCondition();
	}

	public void Dispose()
	{
		for (var i = _conditions.Count - 1; i >= 0; i--)
		{
			_conditions[i].Dispose();
			_conditions[i].IsTrue.Unsubscribe(HandleConditionValueChangedEvent);
		}
		_conditions.Clear();
	}

	private void HandleConditionValueChangedEvent(bool isTrue) => UpdateCondition();

	private void UpdateCondition() => _isTrue.Value = _conditions.TrueForAll(x => x.IsTrue.Value);
}