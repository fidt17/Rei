using System.Globalization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;

namespace ReiEditor.Tests.Models.Services.Assets.Scripting.Serialization;

/// <summary>
/// Verifies serialized value compatibility and default parsing, including float literal regression coverage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Serialization")]
public sealed class SerializedTypeExtensionsTests
{
    /// <summary>
    /// Integer and enum accept all integral CLR types; float accepts only its documented numeric subset.
    /// </summary>
    [Fact]
    public void NumericCompatibilityDistinguishesIntegralAndFloatingPointValues()
    {
        object[] integers = [(sbyte)-1, (byte)1, (short)-2, (ushort)2, -3, 3u, -4L, 4UL];
        foreach (var value in integers)
        {
            Assert.True(SerializedTypeEnum.Integer.IsValidValue(value));
            Assert.True(SerializedTypeEnum.Enum.IsValidValue(value));
            Assert.Equal(value is int or long, SerializedTypeEnum.Float.IsValidValue(value));
        }

        foreach (var value in new object[] { 1f, 2d })
        {
            Assert.False(SerializedTypeEnum.Integer.IsValidValue(value));
            Assert.False(SerializedTypeEnum.Enum.IsValidValue(value));
            Assert.True(SerializedTypeEnum.Float.IsValidValue(value));
        }

        foreach (var value in new object?[] { null, 1m, "1", true, '1' })
        {
            Assert.False(SerializedTypeEnum.Integer.IsValidValue(value));
            Assert.False(SerializedTypeEnum.Enum.IsValidValue(value));
            Assert.False(SerializedTypeEnum.Float.IsValidValue(value));
        }
    }

    /// <summary>
    /// Strings and booleans require matching CLR types while custom and collection accept arbitrary payloads.
    /// </summary>
    [Fact]
    public void ReferenceAndBooleanCompatibilityUsesDeclaredType()
    {
        foreach (var value in new object?[] { null, "", "true", true, false, 1, new object(), new[] { 1 } })
        {
            Assert.Equal(value is string, SerializedTypeEnum.String.IsValidValue(value));
            Assert.Equal(value is bool, SerializedTypeEnum.Boolean.IsValidValue(value));
            Assert.True(SerializedTypeEnum.Custom.IsValidValue(value));
            Assert.True(SerializedTypeEnum.Collection.IsValidValue(value));
        }
    }

    /// <summary>
    /// Defaults retain their CLR types; null and invalid numeric text fall back without losing string content.
    /// </summary>
    [Fact]
    public void DefaultValuesAndFallbacksMatchSerializedTypes()
    {
        var defaults = new (SerializedTypeEnum Type, object? Expected)[]
        {
            (SerializedTypeEnum.Integer, 0), (SerializedTypeEnum.Enum, 0),
            (SerializedTypeEnum.Float, 0f), (SerializedTypeEnum.Boolean, false),
            (SerializedTypeEnum.String, ""), (SerializedTypeEnum.Custom, null),
            (SerializedTypeEnum.Collection, null)
        };
        foreach (var (type, expected) in defaults)
        {
            Assert.Equal(expected, type.GetDefaultValue());
            Assert.Equal(expected, type.ParseDefaultValue(null));
            Assert.Equal(type == SerializedTypeEnum.String ? "not a value" : expected, type.ParseDefaultValue("not a value"));
        }
        Assert.Equal(0, SerializedTypeEnum.Integer.ParseDefaultValue("2147483648"));
    }

    /// <summary>
    /// Valid default text parses into the expected CLR value under an isolated invariant culture.
    /// </summary>
    [Theory]
    [InlineData(SerializedTypeEnum.Integer, "-42", -42)]
    [InlineData(SerializedTypeEnum.Enum, "7", 7)]
    [InlineData(SerializedTypeEnum.Boolean, "TrUe", true)]
    [InlineData(SerializedTypeEnum.String, "  hello  ", "  hello  ")]
    [InlineData(SerializedTypeEnum.Float, "-1.25", -1.25f)]
    public void ParsesValidDefaults(SerializedTypeEnum type, string text, object expected)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.Equal(expected, type.ParseDefaultValue(text));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    /// <summary>
    /// A C++ float suffix must not multiply an integer-valued literal by ten.
    /// </summary>
    [Fact]
    public void FloatSuffixPreservesNumericValue()
    {
        Assert.Equal(1f, Assert.IsType<float>(SerializedTypeEnum.Float.ParseDefaultValue("1f")));
    }

    /// <summary>
    /// Invalid and undefined type tags reject validation and default lookup instead of inventing values.
    /// </summary>
    [Theory]
    [InlineData(SerializedTypeEnum.Invalid)]
    [InlineData((SerializedTypeEnum)999)]
    public void UnsupportedTypeTagsThrow(SerializedTypeEnum type)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => type.IsValidValue(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => type.GetDefaultValue());
        Assert.Throws<ArgumentOutOfRangeException>(() => type.ParseDefaultValue("1"));
    }
}
