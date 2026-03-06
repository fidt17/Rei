#pragma once
#include "glm/vec2.hpp"

namespace rei::render
{
    struct Vertex
    {
        glm::vec3 Position{};
        glm::vec3 Normal{};
        glm::vec2 TexCoords{};
    };
}
