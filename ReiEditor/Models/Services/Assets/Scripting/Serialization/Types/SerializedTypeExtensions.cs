using System;
using System.Globalization;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;

public static class SerializedTypeExtensions
{
    public static bool IsValidValue(this SerializedTypeEnum type, object? value)
    {
        return type switch
        {
            SerializedTypeEnum.Integer => value != null && value.GetType().IsInteger(),
            SerializedTypeEnum.String => value is string,
            SerializedTypeEnum.Boolean => value is bool,
            SerializedTypeEnum.Float => value is int or float or double or long,
            SerializedTypeEnum.Enum => value != null && value.GetType().IsInteger(),
            SerializedTypeEnum.Custom => true,
            SerializedTypeEnum.Collection => true,
            SerializedTypeEnum.Invalid => throw new ArgumentOutOfRangeException(nameof(type), type, null),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    public static object? ParseDefaultValue(this SerializedTypeEnum type, string? value)
    {
        try
        {
            if (value == null) return type.GetDefaultValue();
            
            return type switch
            {
                SerializedTypeEnum.Integer => int.Parse(value),
                SerializedTypeEnum.String => value,
                SerializedTypeEnum.Boolean => bool.Parse(value),
                SerializedTypeEnum.Float => ParseFloatDefault(value),
                SerializedTypeEnum.Enum => int.Parse(value),
                SerializedTypeEnum.Custom => null,
                SerializedTypeEnum.Collection => null,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return type.GetDefaultValue();
        }
    }

    private static float ParseFloatDefault(string value)
    {
        var text = value.AsSpan().Trim();
        if (!text.IsEmpty && text[^1] is 'f' or 'F') text = text[..^1];

        return float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    public static object? GetDefaultValue(this SerializedTypeEnum type)
    {
        return type switch
        {
            SerializedTypeEnum.Integer => 0,
            SerializedTypeEnum.String => "",
            SerializedTypeEnum.Boolean => false,
            SerializedTypeEnum.Float => 0f,
            SerializedTypeEnum.Enum => 0,
            SerializedTypeEnum.Custom => null,
            SerializedTypeEnum.Collection => null,
            SerializedTypeEnum.Invalid => throw new ArgumentOutOfRangeException(nameof(type), type, null),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}
