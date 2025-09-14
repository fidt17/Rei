#include "pch.h"
#include "Color.h"

rei::render::Color::operator std::string() const
{
    return "[" + STRING(r) + ", " + STRING(g) + ", " + STRING(b) + ", " + STRING(a) + "]";
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

    Color c = {0.0f, 0.0f, 0.0f, 1.0f};

    auto parseHexByte = [&](const char* str, unsigned int& value)
    {
        auto result = std::from_chars(str, str + 2, value, 16);
        if (result.ec != std::errc())
        {
            LOG_ERROR("Invalid color hex value: {}", hex)
            return Color(0,0,0,1);
        }
    };

    if (cleanHex.length() == 3)
    {
        unsigned int r, g, b;
        parseHexByte((std::string(2, cleanHex[0])).c_str(), r);
        parseHexByte((std::string(2, cleanHex[1])).c_str(), g);
        parseHexByte((std::string(2, cleanHex[2])).c_str(), b);
        c = {r / 255.0f, g / 255.0f, b / 255.0f, 1.0f};
    }
    else if (cleanHex.length() == 6)
    {
        unsigned int r, g, b;
        parseHexByte(cleanHex.substr(0, 2).c_str(), r);
        parseHexByte(cleanHex.substr(2, 2).c_str(), g);
        parseHexByte(cleanHex.substr(4, 2).c_str(), b);
        c = {r / 255.0f, g / 255.0f, b / 255.0f, 1.0f};
    }
    else if (cleanHex.length() == 8)
    {
        unsigned int r, g, b, a;
        parseHexByte(cleanHex.substr(0, 2).c_str(), r);
        parseHexByte(cleanHex.substr(2, 2).c_str(), g);
        parseHexByte(cleanHex.substr(4, 2).c_str(), b);
        parseHexByte(cleanHex.substr(6, 2).c_str(), a);
        c = {r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f};
    }

    return c;
}

rei::render::Color::Color(const f32 r, const f32 g, const f32 b, const f32 a)
    : r(r), g(g), b(b), a(a)
{
}
