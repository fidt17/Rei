#pragma once
#include "Core.h"
#include "glm/vec3.hpp"

namespace rei::math
{
    struct Vector3
    {
        SERIALIZABLE_BODY(Vector3)

        REI_API explicit Vector3(f32 x, f32 y = 0, f32 z = 0);
        REI_API explicit Vector3(glm::vec3 vec3);

        SERIALIZE f32 x = 0;
        SERIALIZE f32 y = 0;
        SERIALIZE f32 z = 0;

        constexpr operator glm::vec3() const { return glm::vec3(x, y, z); }
        operator std::string() const;

        Vector3 operator-() const;

        Vector3& operator+=(const glm::vec<3, float>& vec);
        Vector3& operator+=(const Vector3& vec);

        Vector3& operator-=(const glm::vec<3, float>& vec);
        Vector3& operator-=(const Vector3& vec);

        Vector3& operator*=(float value);
        Vector3& operator/=(float value);
        
        Vector3 operator+(const Vector3& vec) const;
        Vector3 operator-(const Vector3& vec) const;
        Vector3 operator*(float value) const;
        Vector3 operator/(float value) const;

        f32 Length() const;

        static Vector3 Right();
        static Vector3 Left();

        static Vector3 Up();
        static Vector3 Down();

        static Vector3 Forward();
        static Vector3 Backwards();

        static float Dot(const Vector3& a, const Vector3& b);
        static Vector3 Cross(const Vector3& a, const Vector3& b);
    };
}
