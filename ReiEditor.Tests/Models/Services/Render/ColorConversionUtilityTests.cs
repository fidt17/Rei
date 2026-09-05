using Avalonia.Media;
using ReiEditor.Models.Services.Render;

namespace ReiEditor.Tests.Models.Services.Render;

/// <summary>Verifies color conversion using known color values, boundary inputs, and independent hex expectations.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "ColorMath")]
public sealed class ColorConversionUtilityTests
{
    /// <summary>RGB and HSV conversions agree with known primary, secondary, black, and grayscale colors.</summary>
    [Theory]
    [InlineData(1f, 0f, 0f, 0f, 1f, 1f)]
    [InlineData(1f, 1f, 0f, 60f, 1f, 1f)]
    [InlineData(0f, 1f, 0f, 120f, 1f, 1f)]
    [InlineData(0f, 1f, 1f, 180f, 1f, 1f)]
    [InlineData(0f, 0f, 1f, 240f, 1f, 1f)]
    [InlineData(1f, 0f, 1f, 300f, 1f, 1f)]
    [InlineData(0.5f, 0.5f, 0.5f, 0f, 0f, 0.5f)]
    [InlineData(0f, 0f, 0f, 0f, 0f, 0f)]
    public void ConversionsMatchKnownColorCoordinates(float r, float g, float b, float h, float s, float v)
    {
        ColorConversionUtility.RgbToHsv(r, g, b, out var actualH, out var actualS, out var actualV);
        Assert.Equal(h, actualH, precision: 4);
        Assert.Equal(s, actualS, precision: 4);
        Assert.Equal(v, actualV, precision: 4);

        ColorConversionUtility.HsvToRgb(h, s, v, out var actualR, out var actualG, out var actualB);
        Assert.Equal(r, actualR, precision: 4);
        Assert.Equal(g, actualG, precision: 4);
        Assert.Equal(b, actualB, precision: 4);
    }

    /// <summary>Channel clamping maps invalid floating point values to zero and bounds finite values to the unit interval.</summary>
    [Theory]
    [InlineData(float.NaN, 0f)]
    [InlineData(float.PositiveInfinity, 0f)]
    [InlineData(float.NegativeInfinity, 0f)]
    [InlineData(-1f, 0f)]
    [InlineData(0.25f, 0.25f)]
    [InlineData(2f, 1f)]
    public void ChannelClampHandlesNonFiniteAndOutOfRangeInput(float input, float expected)
    {
        Assert.Equal(expected, ColorConversionUtility.Clamp01(input));
    }

    /// <summary>Hue wraps through a full circle and rejects non-finite values.</summary>
    [Theory]
    [InlineData(-60f, 300f)]
    [InlineData(360f, 0f)]
    [InlineData(780f, 60f)]
    [InlineData(float.NaN, 0f)]
    [InlineData(float.PositiveInfinity, 0f)]
    public void HueNormalizationWrapsAngles(float input, float expected)
    {
        Assert.Equal(expected, ColorConversionUtility.ClampHue(input));
    }

    /// <summary>RGBA conversion clamps channels, rounds to bytes, and writes alpha after RGB in hex output.</summary>
    [Fact]
    public void ByteAndHexConversionUseRgbaOrder()
    {
        Assert.Equal(Color.FromArgb(128, 255, 0, 0), ColorConversionUtility.FromRgba01(2f, -1f, float.NaN, 0.5f));
        Assert.Equal("#FF000080", ColorConversionUtility.ToHex(1f, 0f, 0f, 0.5f));
    }

    /// <summary>Hex parsing accepts optional hash, whitespace, casing, and RGB or RGBA with an opaque default alpha.</summary>
    [Theory]
    [InlineData("#FF0000", 255, 0, 0, 255)]
    [InlineData(" 00ff0080 ", 0, 255, 0, 128)]
    [InlineData("#0000ff00", 0, 0, 255, 0)]
    public void HexParsingReadsChannels(string text, int red, int green, int blue, int alpha)
    {
        Assert.True(ColorConversionUtility.TryParseHex(text, out var r, out var g, out var b, out var a));
        Assert.Equal(red / 255f, r);
        Assert.Equal(green / 255f, g);
        Assert.Equal(blue / 255f, b);
        Assert.Equal(alpha / 255f, a);
    }

    /// <summary>Malformed hex input is rejected and leaves the documented black/opaque defaults.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#FFF")]
    [InlineData("#123456789")]
    [InlineData("GG0000")]
    [InlineData("FF0000GG")]
    public void HexParsingRejectsInvalidInput(string? text)
    {
        Assert.False(ColorConversionUtility.TryParseHex(text, out var r, out var g, out var b, out var a));
        Assert.Equal(0f, r);
        Assert.Equal(0f, g);
        Assert.Equal(0f, b);
        Assert.Equal(1f, a);
    }
}
