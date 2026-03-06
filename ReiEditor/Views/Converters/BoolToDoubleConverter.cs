using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ReiEditor.Views.Converters;

public class BoolToDoubleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolValue = value is bool b && b;

        if (parameter is string paramText)
        {
            var parts = paramText.Split(',');
            if (parts.Length >= 2 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var trueValue) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var falseValue))
            {
                return boolValue ? trueValue : falseValue;
            }
        }

        return boolValue ? 1d : 0d;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}
