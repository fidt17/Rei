#pragma once
#include "Core.h"

#include "glm/vec2.hpp"
#include "glm/fwd.hpp"

namespace rei::math
{
    struct Vector2
    {
        SERIALIZABLE_BODY(Vector2)

        SERIALIZE f32 x = 0;
        SERIALIZE f32 y = 0;

        REI_API Vector2(f32 x, f32 y = 0);
        REI_API Vector2(glm::vec2 vec2);

        REI_API constexpr operator glm::vec2() const { return glm::vec2(x, y); }
        REI_API operator std::string() const;

        REI_API Vector2 operator-() const;

        REI_API Vector2& operator+=(const glm::vec<2, f32>& vec);
        REI_API Vector2& operator+=(const Vector2& vec);

        REI_API Vector2& operator-=(const glm::vec<2, f32>& vec);
        REI_API Vector2& operator-=(const Vector2& vec);

        template <typename T>
        REI_API Vector2& operator*=(T value)
        {
            x *= value;
            y *= value;

            return *this;
        }

        template <typename T>
        REI_API Vector2& operator/=(T value)
        {
            x /= value;
            y /= value;

            return *this;
        }

        REI_API Vector2 operator+(const Vector2& vec) const;
        REI_API Vector2 operator-(const Vector2& vec) const;

        template <typename T>
        REI_API Vector2 operator*(const T value) const
        {
            return Vector2(x * value, y * value);
        }

        template <typename T>
        REI_API Vector2 operator/(const T value) const
        {
            return Vector2(x / value, y / value);
        }

        REI_API Vector2 operator*(const Vector2& vec) const;
        REI_API Vector2 operator/(const Vector2& vec) const;

        REI_API f32 operator[](i32 idx) const;

        REI_API f32 Length() const;

        REI_API static Vector2 One();

        REI_API static Vector2 Right();
        REI_API static Vector2 Left();

        REI_API static Vector2 Up();
        REI_API static Vector2 Down();

        REI_API static Vector2 Max();
        REI_API static Vector2 Min();

        REI_API static f32 Dot(const Vector2& a, const Vector2& b);
        REI_API static Vector2 Average(const Vector2& a, const Vector2& b);
        REI_API static f32 Length(const Vector2& v);
        REI_API static Vector2 Normalize(const Vector2& v);
        REI_API static f32 Distance(const Vector2& a, const Vector2& b);
        REI_API static Vector2 Abs(const Vector2& v);
    };

    template <typename T>
    REI_API Vector2 operator*(T value, const Vector2& vector)
    {
        return vector * value;
    }
}
