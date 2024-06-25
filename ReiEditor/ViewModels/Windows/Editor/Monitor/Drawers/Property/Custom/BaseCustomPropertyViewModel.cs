using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

public abstract class BaseCustomPropertyViewModel : BaseViewModel
{
    public PropertyNameViewModel PropertyName { get; }

    private IEnumerable<SerializedProperty>? _nestedProperties
    {
        get
        {
            if (_property.Value is Dictionary<string, SerializedProperty> map)
            {
                return map.Values.ToList();
            }

            return null;
        }
    }

    private readonly SerializedProperty _property;
    
#pragma warning disable CS8618
    protected BaseCustomPropertyViewModel() { }
#pragma warning restore CS8618

    protected BaseCustomPropertyViewModel(SerializedProperty property)
    {
        if (property.Type != SerializedTypeEnum.Custom) throw new Exception($"Invalid property type. Expected {SerializedTypeEnum.Custom}. Actual {property.Type}");
            
        _property = property;
    
        PropertyName = new(property);
        _property.ValueChangedEvent += HandlePropertyValueChangedEvent;

        Dispatcher.UIThread.Invoke(() =>
        {
            HandlePropertyValueChangedEvent(_property.Value);
        });
    }
    
    public override void Dispose()
    {
        base.Dispose();
            
        _property.ValueChangedEvent -= HandlePropertyValueChangedEvent;
    }

    protected SerializedProperty? GetNestedProperty(string name)
    {
        var property = _nestedProperties?.FirstOrDefault(x => x.Name == name);
        return property;
    }

    protected abstract void HandlePropertyValueChangedEvent(object? value);
}