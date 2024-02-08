using System;
using ReiEditor.Models.Services.Assets.Behaviours.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

public class StringPropertyViewModel : BaseViewModel
{
    public string PropertyName { get; }
    
    #region Value

    private string _value = "";
    public string Value
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
    public StringPropertyViewModel() { }
#pragma warning restore CS8618

    public StringPropertyViewModel(SerializedProperty property)
    {
        if (property.Type != SerializedTypeEnum.String) throw new Exception($"Invalid property type. Expected {SerializedTypeEnum.String}. Actual {property.Type}");
        
        _property = property;

        PropertyName = PropertyViewUtils.ConvertPropertyNameToEditorStyle(property.Name);
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
        if (value is string str)
        {
            Value = str;
        }
    }
}