using System;
using System.Globalization;
using Avalonia.Data.Converters;
using ReiEditor.Models.Services.Logging;

namespace ReiEditor.Views.Converters;

public class LogLevelEnumToBoolConverter : IValueConverter
{
	public LogLevelEnum Level { get; set; } 
	
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var level = (LogLevelEnum)(value ?? LogLevelEnum.Info);
		return level == Level;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}