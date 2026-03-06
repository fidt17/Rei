using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ReactiveUI;

namespace ReiEditor.Utils.Common;

public class ObservableField<T> : ReactiveObject
{
	public event Action<T>? ChangedEvent;
	
	private T? _value;
	public T Value
	{
		get => _value ?? throw new NullReferenceException();
		set
		{
			if (SetField(ref _value, value))
			{
				ChangedEvent?.Invoke(Value);
			}
		}
	}

	public ObservableField(T defaultValue)
	{
		_value = defaultValue;
	}

	public T Get() => Value;
	public void Set(T value) => Value = value;

	public void Dispose()
	{
		if (_value is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}
	
    private bool SetField<T_F>(ref T_F field, T_F value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T_F>.Default.Equals(field, value)) return false;
        field = value;
        
        this.RaisePropertyChanged(propertyName);
        return true;
    }
}
