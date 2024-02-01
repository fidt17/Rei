using System;

namespace ReiEditor.Models.Services.Assets.Behaviours.Types;

public static class SerializedTypeExtensions
{
    public static bool IsValidValue(this SerializedTypeEnum type, object? value)
    {
        return type switch
        {
            SerializedTypeEnum.String => value is string,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    public static object GetDefaultValue(this SerializedTypeEnum type)
    {
        return type switch
        {
            SerializedTypeEnum.String => new Random().Next().ToString(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}