#pragma once
#include "Core.h"
#include "glm/vec3.hpp"

namespace rei::math
{
    struct Vector3
    {
        SERIALIZABLE_BODY(Vector3)

        explicit Vector3(f32 x, f32 y = 0, f32 z = 0);
        explicit Vector3(glm::vec3 vec3);

        SERIALIZE f32 x;
        SERIALIZE f32 y;
        SERIALIZE f32 z;

        constexpr operator glm::vec3() const { return glm::vec3(x, y, z); }
        operator std::string() const { return "[" + STRING(x) + ", " + STRING(y) + ", " +STRING(z) + "]"; }

        Vector3 operator-() const;

        static Vector3 Right();
        static Vector3 Left();

        static Vector3 Up();
        static Vector3 Down();

        static Vector3 Forward();
        static Vector3 Backwards();
    };
}
