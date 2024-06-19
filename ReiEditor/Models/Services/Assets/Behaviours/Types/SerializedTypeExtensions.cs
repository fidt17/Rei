using System;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Assets.Behaviours.Types;

public static class SerializedTypeExtensions
{
    public static bool IsValidValue(this SerializedTypeEnum type, object? value)
    {
        return type switch
        {
            SerializedTypeEnum.Integer => value != null && value.GetType().IsInteger(),
            SerializedTypeEnum.String => value is string,
            SerializedTypeEnum.Boolean => value is bool,
            SerializedTypeEnum.Float => value != null && value is float || value is double,
            SerializedTypeEnum.Invalid => throw new ArgumentOutOfRangeException(nameof(type), type, null),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    public static object GetDefaultValue(this SerializedTypeEnum type)
    {
        return type switch
        {
            SerializedTypeEnum.Integer => 0,
            SerializedTypeEnum.String => "",
            SerializedTypeEnum.Boolean => false,
            SerializedTypeEnum.Float => 0f,
            SerializedTypeEnum.Invalid => throw new ArgumentOutOfRangeException(nameof(type), type, null),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}