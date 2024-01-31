namespace ReiEditor.Models.Services.Assets.Behaviours.Types;

public class StringSerializedType : ISerializedType
{
    public bool IsValidValue(object? value)
    {
        return value is string;
    }

    public object GetDefaultValue()
    {
        return "";
    }
}