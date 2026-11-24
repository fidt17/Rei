#include "pch.h"
#include "Color.h"

rei::render::Color::operator std::string() const
{
    return "[" + STRING(r) + ", " + STRING(g) + ", " + STRING(b) + ", " + STRING(a) + "]";
}

rei::render::Color rei::render::Color::operator*(const Color& col) const
{
    return Color(
        r * col.r,
        g * col.g,
        b * col.b,
        a * col.a);
}

rei::render::Color rei::render::Color::Clear()
{
    return Color(0,0,0,0);
}

rei::render::Color rei::render::Color::White()
{
    return Color(1,1,1,1);
}

rei::render::Color rei::render::Color::Black()
{
    return Color(0,0,0,1);
}

rei::render::Color rei::render::Color::Red()
{
    return Color(1,0,0,1);
}

rei::render::Color rei::render::Color::Green()
{
    return Color(0,1,0,1);
}

rei::render::Color rei::render::Color::Blue()
{
    return Color(0,0,1,1);
}

rei::render::Color rei::render::Color::ToLinear() const
{
    const auto decode = [](const f32 value)
    {
        return value <= 0.04045f ? value / 12.92f : std::pow((value + 0.055f) / 1.055f, 2.4f);
    };
    return Color(decode(r), decode(g), decode(b), a);
}

rei::render::Color rei::render::Color::FromHex(const std::string& hex)
{
    std::string cleanHex = hex;

    if (!cleanHex.empty() && cleanHex[0] == '#')
    {
        cleanHex = cleanHex.substr(1);
    }

    if (cleanHex.length() != 3 && cleanHex.length() != 6 && cleanHex.length() != 8)
    {
        throw std::invalid_argument("Invalid hex color format");
    }

    const auto parseHexByte = [](const char* first)
    {
        u32 value = 0;
        const auto last = first + 2;
        const auto result = std::from_chars(first, last, value, 16);
        if (result.ec != std::errc() || result.ptr != last)
        {
            throw std::invalid_argument("Invalid hex color value");
        }
        return value / 255.0f;
    };

    if (cleanHex.length() == 3)
    {
        return {
            parseHexByte(std::string(2, cleanHex[0]).data()),
            parseHexByte(std::string(2, cleanHex[1]).data()),
            parseHexByte(std::string(2, cleanHex[2]).data()),
            1.0f
        };
    }

    const auto digits = cleanHex.data();
    return {
        parseHexByte(digits),
        parseHexByte(digits + 2),
        parseHexByte(digits + 4),
        cleanHex.length() == 8 ? parseHexByte(digits + 6) : 1.0f
    };
}

rei::render::Color::Color(const f32 r, const f32 g, const f32 b, const f32 a)
    : r(r), g(g), b(b), a(a)
{
}
