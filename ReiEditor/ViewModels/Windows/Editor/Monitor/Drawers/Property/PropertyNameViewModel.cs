using ReiEditor.Models.Services.Components;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

public class PropertyNameViewModel : BaseViewModel
{
    public string Value { get; }

    public PropertyNameViewModel(SerializedProperty property)
    {
        Value = PropertyViewUtils.ConvertPropertyNameToEditorStyle(property.Name);
    }
}