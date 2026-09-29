using ReiEditor.Models.Services.Components;

namespace ReiEditor.Models.Services.Assets.Scripting.Serialization;

public interface ISerializedPropertiesService
{
    SerializedProperty Create(string name, SerializableObjectInfo.SerializedPropertyData propertyData, SerializedProperty? parentProperty);
    void Refresh(SerializedProperty property);
    void ApplyValue(SerializedProperty property, object? value);
}
