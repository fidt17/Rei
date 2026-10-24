#pragma once
#include <array>
#include "Vertex.h"

namespace rei::render
{
    struct Face
    {
        std::array<Vertex, 3> Vertices {};
    };
}
