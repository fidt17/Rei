#include "pch.h"
#include "Color.h"

rei::render::Color::operator std::string() const
{
    return "[" + STRING(r) + ", " + STRING(g) + ", " + STRING(b) + ", " + STRING(a) + "]";
}

rei::render::Color::Color(const f32 r, const f32 g, const f32 b, const f32 a)
    : r(r), g(g), b(b), a(a)
{
}
