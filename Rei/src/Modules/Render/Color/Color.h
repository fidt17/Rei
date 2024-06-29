#pragma once

namespace rei::render
{
    struct Color
    {
        SERIALIZABLE_BODY(Color)
        
        explicit Color(f32 r, f32 g = 0, f32 b = 0, f32 a = 1);

        SERIALIZE f32 r;
        SERIALIZE f32 g;
        SERIALIZE f32 b;
        SERIALIZE f32 a;

        operator std::string() const;
    };
}
