#pragma once

namespace rei::render
{
    struct REI_API Color
    {
        SERIALIZABLE_BODY(Color)
        
        explicit Color(f32 r, f32 g = 0, f32 b = 0, f32 a = 1);

        SERIALIZE f32 r = 1;
        SERIALIZE f32 g = 1;
        SERIALIZE f32 b = 1;
        SERIALIZE f32 a = 1;

        operator std::string() const;
    };
}
