namespace ReiEditor.Models.Services.Assets.Behaviours.Types;

public interface ISerializedType
{
    bool IsValidValue(object? value);
    object GetDefaultValue();
}