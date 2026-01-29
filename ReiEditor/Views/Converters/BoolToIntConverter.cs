using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ReiEditor.Views.Converters;

public class BoolToIntConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolValue = value is bool b && b;

        if (parameter is string paramText)
        {
            var parts = paramText.Split(',');
            if (parts.Length >= 2 &&
                int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var trueValue) &&
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var falseValue))
            {
                return boolValue ? trueValue : falseValue;
            }
        }

        return boolValue ? 1 : 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}
