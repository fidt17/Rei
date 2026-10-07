using System;
using System.Globalization;
using System.Text.RegularExpressions;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;

namespace ReiEditor.Models.Services.Assets.Scripting.Serialization;

/// <summary>Source-only numeric editor metadata; never part of asset or native serialization.</summary>
public sealed record SerializedNumericRange(double Minimum, double Maximum, double? Step, bool IsInteger)
{
    private const string NUMBER_PATTERN = @"^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?[fF]?$";

    public double SmallChange => Step ?? (IsInteger ? 1 : Math.Min(Maximum - Minimum, Math.Max((Maximum - Minimum) / 100, Math.Max(FloatSpacing(Minimum), FloatSpacing(Maximum)))));
    public double LargeChange
    {
        get
        {
            var span = Maximum - Minimum;
            if (!SnapToStep) return Math.Max(SmallChange, span / 10);
            return Math.Min(span, Math.Max(1, Math.Round(span / 10 / SmallChange)) * SmallChange);
        }
    }
    public bool SnapToStep => IsInteger || Step != null;
    public string Label => $"{Minimum.ToString("G", CultureInfo.InvariantCulture)} – {Maximum.ToString("G", CultureInfo.InvariantCulture)}";

    public static SerializedNumericRange Parse(string arguments, string sourceType, string? defaultValue)
    {
        var type = SerializedTypeNameParser.GetBaseTypeName(sourceType);
        var integer = type is "i32" or "int";
        if (!integer && type is not ("f32" or "float")) throw new FormatException($"REI_RANGE supports i32/f32 fields, not {sourceType}.");

        var values = arguments.Split(',');
        if (values.Length is not (2 or 3)) throw new FormatException("REI_RANGE expects min, max, and an optional step.");
        var minimum = ParseNumber(values[0], integer);
        var maximum = ParseNumber(values[1], integer);
        if (minimum >= maximum) throw new FormatException("REI_RANGE requires min < max after conversion to the field type.");
        double? step = values.Length == 3 ? ParseNumber(values[2], integer) : null;
        if (step is <= 0 || step > maximum - minimum) throw new FormatException("REI_RANGE step must be positive and no larger than max - min.");

        var initial = defaultValue == null ? 0 : ParseNumber(defaultValue, integer);
        if (initial < minimum || initial > maximum) throw new FormatException("REI_RANGE field default must lie within min/max.");
        return new(minimum, maximum, step, integer);
    }

    private static double ParseNumber(string text, bool integer)
    {
        text = text.Trim();
        if (integer)
        {
            if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
                throw new FormatException($"REI_RANGE expects an i32 integer literal, got '{text}'.");
            return value;
        }

        if (!Regex.IsMatch(text, NUMBER_PATTERN)) throw new FormatException($"REI_RANGE expects a decimal numeric literal, got '{text}'.");
        if (text.EndsWith('f') || text.EndsWith('F')) text = text[..^1];
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !float.IsFinite(number))
            throw new FormatException($"REI_RANGE literal is not a finite f32: '{text}'.");
        return number;
    }

    public double Clamp(double value) => Math.Clamp(value, Minimum, Maximum);

    private static double FloatSpacing(double value)
    {
        var magnitude = (float)Math.Abs(value);
        var next = float.BitIncrement(magnitude);
        return float.IsFinite(next) ? (double)next - magnitude : (double)magnitude - float.BitDecrement(magnitude);
    }

    public double NormalizeSlider(double value)
    {
        value = Clamp(value);
        if (!SnapToStep || value == Minimum || value == Maximum) return value;
        var snapped = Clamp(Minimum + Math.Round((value - Minimum) / SmallChange, MidpointRounding.AwayFromZero) * SmallChange);
        // Keep the upper endpoint reachable when it is not on the regular step grid.
        return Maximum - value < Math.Abs(snapped - value) ? Maximum : snapped;
    }

    public bool TryParseInput(string text, out double value)
    {
        if (IsInteger)
        {
            var valid = int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var integer);
            value = integer;
            return valid;
        }

        var parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
                     double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        return parsed && double.IsFinite(value) && value >= -float.MaxValue && value <= float.MaxValue;
    }

    public object ToFieldValue(double value) => IsInteger ? (object)checked((int)value) : (float)value;
}
