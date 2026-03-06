using System;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

public class StringPropertyViewModel : BasePropertyViewModel<string>
{
    public StringPropertyViewModel() { }

    public StringPropertyViewModel(SerializedProperty property) : base(property) { }

    protected override string ParseValue(object? value)
    {
        if (value is string str)
        {
            return str;
        }
        
        throw new Exception($"Not supported value type: {value}");
    }
}