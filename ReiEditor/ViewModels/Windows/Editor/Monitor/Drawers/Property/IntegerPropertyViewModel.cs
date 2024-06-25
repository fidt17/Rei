using System;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

public class IntegerPropertyViewModel : BasePropertyViewModel<int>
{
    public IntegerPropertyViewModel() { }

    public IntegerPropertyViewModel(SerializedProperty property) : base(property) { }

    protected override int ParseValue(object? value)
    {
        if (value is long l)
        {
            return (int) l;
        }

        if (value is int i)
        {
            return i;
        }
        
        throw new Exception($"Not supported value type: {value}");
    }
}