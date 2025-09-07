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

rei::render::Color::Color(const f32 r, const f32 g, const f32 b, const f32 a)
    : r(r), g(g), b(b), a(a)
{
}
