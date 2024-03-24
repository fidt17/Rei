using System;
using ReiEditor.Models.Services.Assets.Behaviours.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

public class BooleanPropertyViewModel : BaseViewModel
{
    public PropertyNameViewModel PropertyName { get; }
    
    #region Value

    private bool _value;
    public bool Value
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
    public BooleanPropertyViewModel() { }
#pragma warning restore CS8618

    public BooleanPropertyViewModel(SerializedProperty property)
    {
        if (property.Type != SerializedTypeEnum.Boolean) throw new Exception($"Invalid property type. Expected {SerializedTypeEnum.Boolean}. Actual {property.Type}");
        
        _property = property;

        PropertyName = new(property);
        _property.ValueChangedEvent += HandlePropertyValueChangedEvent;
        
        HandlePropertyValueChangedEvent(_property.Value);
    }

    public override void Dispose()
    {
        base.Dispose();
        
        _property.ValueChangedEvent -= HandlePropertyValueChangedEvent;
    }

    private void HandlePropertyValueChangedEvent(object? value)
    {
        if (value is bool v)
        {
            Value = v;
        }
    }
}