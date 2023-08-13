using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ReactiveUI;

namespace ReiEditor.ViewModels.Common;

public abstract class BaseViewModel : ReactiveObject, IDisposable
{
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        
        this.RaisePropertyChanged(propertyName);
        return true;
    }

    public virtual void Dispose() { }
}