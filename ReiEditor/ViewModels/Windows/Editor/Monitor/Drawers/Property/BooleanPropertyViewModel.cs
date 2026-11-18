using System;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

public class BooleanPropertyViewModel : BasePropertyViewModel<bool>
{
    public BooleanPropertyViewModel() { }

    public BooleanPropertyViewModel(SerializedProperty property, Action<object>? commit = null) : base(property, commit) { }
    
    protected override bool ParseValue(object? value)
    {
        if (value is bool v)
        {
            return v;
        }
        
        throw new Exception($"Not supported value type: {value}");
    }
}
