#pragma once
#include "Core.h"
#include "glm/vec3.hpp"

namespace rei::math
{
    struct Vector3
    {
        SERIALIZABLE_BODY(Vector3)
        
        SERIALIZE f32 X;
        SERIALIZE f32 Y;
        SERIALIZE f32 Z;

        operator glm::vec3() const { return glm::vec3(X, Y, Z); }
    };
}
