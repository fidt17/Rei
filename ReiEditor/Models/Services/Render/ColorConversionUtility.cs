using System;
using System.Globalization;
using Avalonia.Media;

namespace ReiEditor.Models.Services.Render;

public static class ColorConversionUtility
{
    public static float Clamp01(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
        if (value < 0f) return 0f;
        if (value > 1f) return 1f;
        return value;
    }

    public static float ClampHue(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;

        var normalized = value % 360f;
        if (normalized < 0f) normalized += 360f;
        return normalized;
    }

    public static Color FromRgba01(float r, float g, float b, float a)
    {
        var rr = (byte)Math.Round(Clamp01(r) * 255f);
        var gg = (byte)Math.Round(Clamp01(g) * 255f);
        var bb = (byte)Math.Round(Clamp01(b) * 255f);
        var aa = (byte)Math.Round(Clamp01(a) * 255f);
        return Color.FromArgb(aa, rr, gg, bb);
    }

    public static void RgbToHsv(float r, float g, float b, out float h, out float s, out float v)
    {
        r = Clamp01(r);
        g = Clamp01(g);
        b = Clamp01(b);

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        v = max;
        s = max <= 0f ? 0f : delta / max;

        if (delta <= 0f)
        {
            h = 0f;
            return;
        }

        if (max == r)
        {
            h = 60f * (((g - b) / delta) % 6f);
        }
        else if (max == g)
        {
            h = 60f * (((b - r) / delta) + 2f);
        }
        else
        {
            h = 60f * (((r - g) / delta) + 4f);
        }

        h = ClampHue(h);
    }

    public static void HsvToRgb(float h, float s, float v, out float r, out float g, out float b)
    {
        h = ClampHue(h);
        s = Clamp01(s);
        v = Clamp01(v);

        var c = v * s;
        var x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
        var m = v - c;

        var rr = 0f;
        var gg = 0f;
        var bb = 0f;

        if (h < 60f)
        {
            rr = c;
            gg = x;
        }
        else if (h < 120f)
        {
            rr = x;
            gg = c;
        }
        else if (h < 180f)
        {
            gg = c;
            bb = x;
        }
        else if (h < 240f)
        {
            gg = x;
            bb = c;
        }
        else if (h < 300f)
        {
            rr = x;
            bb = c;
        }
        else
        {
            rr = c;
            bb = x;
        }

        r = Clamp01(rr + m);
        g = Clamp01(gg + m);
        b = Clamp01(bb + m);
    }

    public static string ToHex(float r, float g, float b, float a)
    {
        var color = FromRgba01(r, g, b, a);
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
    }

    public static bool TryParseHex(string? text, out float r, out float g, out float b, out float a)
    {
        r = 0f;
        g = 0f;
        b = 0f;
        a = 1f;

        if (string.IsNullOrWhiteSpace(text)) return false;

        var raw = text.Trim();
        if (raw.StartsWith("#", StringComparison.Ordinal))
        {
            raw = raw[1..];
        }

        if (raw.Length != 6 && raw.Length != 8) return false;

        if (!byte.TryParse(raw[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rr)) return false;
        if (!byte.TryParse(raw[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var gg)) return false;
        if (!byte.TryParse(raw[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bb)) return false;

        byte aa = 255;
        if (raw.Length == 8 && !byte.TryParse(raw[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out aa))
        {
            return false;
        }

        r = rr / 255f;
        g = gg / 255f;
        b = bb / 255f;
        a = aa / 255f;
        return true;
    }
}
