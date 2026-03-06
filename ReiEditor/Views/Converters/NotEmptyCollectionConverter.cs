using System;
using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ReiEditor.Views.Converters;

public class NotEmptyCollectionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i) return i > 0;
        
        if (value is ICollection collection)
        {
            return collection.Count > 0;
        }

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}