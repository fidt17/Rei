#pragma once
#include "Core.h"

#include "glm/vec3.hpp"
#include "glm/fwd.hpp"

namespace rei::math
{
    struct Vector3
    {
        SERIALIZABLE_BODY(Vector3)

        SERIALIZE f32 x = 0;
        SERIALIZE f32 y = 0;
        SERIALIZE f32 z = 0;

        REI_API Vector3(f32 x, f32 y = 0, f32 z = 0);
        REI_API Vector3(glm::vec3 vec3);

        REI_API constexpr operator glm::vec3() const { return glm::vec3(x, y, z); }
        REI_API operator std::string() const;

        REI_API Vector3 operator-() const;

        REI_API Vector3& operator+=(const glm::vec<3, float>& vec);
        REI_API Vector3& operator+=(const Vector3& vec);

        REI_API Vector3& operator-=(const glm::vec<3, float>& vec);
        REI_API Vector3& operator-=(const Vector3& vec);

        REI_API Vector3& operator*=(float value);
        REI_API Vector3& operator/=(float value);

        REI_API Vector3 operator+(const Vector3& vec) const;
        REI_API Vector3 operator-(const Vector3& vec) const;
        
        REI_API Vector3 operator*(float value) const;
        REI_API Vector3 operator/(float value) const;

        REI_API Vector3 operator*(const Vector3& vec) const;
        REI_API Vector3 operator/(const Vector3& vec) const;

        REI_API Vector3 Transform(const glm::mat4& m) const;
        
        REI_API f32 operator[](i32 idx) const;

        REI_API f32 Length() const;

        REI_API static Vector3 One();

        REI_API static Vector3 Right();
        REI_API static Vector3 Left();

        REI_API static Vector3 Up();
        REI_API static Vector3 Down();

        REI_API static Vector3 Forward();
        REI_API static Vector3 Backwards();

        REI_API static Vector3 Max();
        REI_API static Vector3 Min();

        REI_API static float Dot(const Vector3& a, const Vector3& b);
        REI_API static Vector3 Cross(const Vector3& a, const Vector3& b);
        REI_API static Vector3 Average(const Vector3& a, const Vector3& b);
        REI_API static f32 Length(const Vector3& v);
        REI_API static Vector3 Normalize(const Vector3& v);
        REI_API static f32 Distance(const Vector3& a, const Vector3& b);
    };
}
