using System;
using ReiEditor.Models.Services.Assets.Behaviours.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

public class FloatPropertyViewModel : BaseViewModel
{
    public PropertyNameViewModel PropertyName { get; }
    
    #region Value

    private float _value;
    public float Value
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
    public FloatPropertyViewModel() { }
#pragma warning restore CS8618

    public FloatPropertyViewModel(SerializedProperty property)
    {
        if (property.Type != SerializedTypeEnum.Float) throw new Exception($"Invalid property type. Expected {SerializedTypeEnum.Float}. Actual {property.Type}");
        
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
        if (value is float f)
        {
            Value = f;
        }
        else if (value is double d)
        {
            Value = (float) d;
        }
        else
        {
            Value = 0;
            throw new Exception($"Not supported value type: {value}");
        }
    }
}