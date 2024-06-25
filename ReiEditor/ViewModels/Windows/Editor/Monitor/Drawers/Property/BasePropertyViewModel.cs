using ReiEditor.Models.Services.Components;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

public abstract class BasePropertyViewModel<T> : BaseViewModel
{
    public PropertyNameViewModel PropertyName { get; }
    
    #region Value

    private T _value = default!;
    public T Value
    {
        get => _value;
        set
        {
            if (!SetField(ref _value, value)) return;
            _property.Value = _value;
        }
    }

    #endregion
    
    private readonly SerializedProperty _property;
 
#pragma warning disable CS8618
    protected BasePropertyViewModel() { }
#pragma warning restore CS8618

    protected BasePropertyViewModel(SerializedProperty property)
    {
        _property = property;
 
        PropertyName = new PropertyNameViewModel(property);
        _property.ValueChangedEvent += HandlePropertyValueChangedEvent;

        HandlePropertyValueChangedEvent(_property.Value);
    }
 
    public override void Dispose()
    {
        base.Dispose();
         
        _property.ValueChangedEvent -= HandlePropertyValueChangedEvent;
    }

    private void HandlePropertyValueChangedEvent(object? value) => Value = ParseValue(value);

    protected abstract T ParseValue(object? value);
}