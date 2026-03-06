using System;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

public class FloatPropertyViewModel : BasePropertyViewModel<float>
{
    public FloatPropertyViewModel() { }

    public FloatPropertyViewModel(SerializedProperty property) : base(property) { }

    protected override float ParseValue(object? value)
    {
        if (value is float f)
        {
            return f;
        }

        if (value is double d)
        {
            return (float) d;
        }
        
        throw new Exception($"Not supported value type: {value}");
    }
}