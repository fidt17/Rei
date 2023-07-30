using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ReactiveUI;

namespace Editor.ViewModels;

public abstract class BaseViewModel : ReactiveObject
{
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        
        this.RaisePropertyChanged(propertyName);
        return true;
    }
}