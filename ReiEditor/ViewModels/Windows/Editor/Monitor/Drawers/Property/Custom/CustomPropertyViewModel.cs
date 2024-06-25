using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Utils.Common;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Utils;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

public class CustomPropertyViewModel : BaseViewModel
{
    public PropertyNameViewModel PropertyName { get; }
    
    public ObservableCollection<BaseViewModel> Value { get; } = new();
    public ObservableField<bool> Expanded { get; } = new(false);
    
    private readonly SerializedProperty _property;

#pragma warning disable CS8618
    public CustomPropertyViewModel() { }
#pragma warning restore CS8618

    public CustomPropertyViewModel(SerializedProperty property)
    {
        if (property.Type != SerializedTypeEnum.Custom) throw new Exception($"Invalid property type. Expected {SerializedTypeEnum.Custom}. Actual {property.Type}");
        
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
    
    public void SwitchExpandState() => Expanded.Value = !Expanded.Value;

    private void HandlePropertyValueChangedEvent(object? value)
    {
        if (value is Dictionary<string, SerializedProperty> subProperties)
        {
            Value.ClearAndDispose();
            
            foreach (var subProperty in subProperties)
            {
                Value.Add(PropertyViewUtils.CreatePropertyViewModel(subProperty.Value));
            }
        }
        else
        {
            throw new Exception($"Not supported value type: {value}");
        }
    }
}