#pragma once

namespace rei::render
{
    struct REI_API Color
    {
        SERIALIZABLE_BODY(Color)

        Color(f32 r, f32 g = 0, f32 b = 0, f32 a = 1);

        SERIALIZE f32 r = 1;
        SERIALIZE f32 g = 1;
        SERIALIZE f32 b = 1;
        SERIALIZE f32 a = 1;

        static Color Lerp(const Color& from, const Color& to, f32 t)
        {
            return Color(std::lerp(from.r, to.r, t), std::lerp(from.g, to.g, t), std::lerp(from.b, to.b, t), std::lerp(from.a, to.a, t));
        }

        operator std::string() const;
        
        Color operator*(const Color& col) const;

        static Color Clear();
        static Color White();
        static Color Black();
        static Color Red();
        static Color Green();
        static Color Blue();

        static Color FromHex(const std::string& hex);
    };
}
