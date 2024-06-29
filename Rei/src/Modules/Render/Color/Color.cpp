#include "pch.h"
#include "Color.h"

rei::render::Color::operator std::string() const
{
    return "[" + STRING(r) + ", " + STRING(g) + ", " + STRING(b) + ", " + STRING(a) + "]";
}
