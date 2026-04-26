#include "pch.h"

#include "Vector2.h"

namespace rei::math
{
    Vector2::Vector2(const f32 x, const f32 y)
        : x(x), y(y)
    {
    }

    Vector2::Vector2(const glm::vec2 vec2)
        : x(vec2.x), y(vec2.y)
    {
    }

    Vector2::operator std::string() const
    {
        return "[" + STRING(x) + ", " + STRING(y) + "]";
    }

    Vector2 Vector2::operator-() const
    {
        return Vector2(-x, -y);
    }

    Vector2& Vector2::operator+=(const glm::vec<2, f32>& vec)
    {
        x += vec.x;
        y += vec.y;

        return *this;
    }

    Vector2& Vector2::operator+=(const Vector2& vec)
    {
        x += vec.x;
        y += vec.y;

        return *this;
    }

    Vector2& Vector2::operator-=(const glm::vec<2, f32>& vec)
    {
        x -= vec.x;
        y -= vec.y;

        return *this;
    }

    Vector2& Vector2::operator-=(const Vector2& vec)
    {
        x -= vec.x;
        y -= vec.y;

        return *this;
    }

    Vector2 Vector2::operator+(const Vector2& vec) const
    {
        return Vector2(x + vec.x, y + vec.y);
    }

    Vector2 Vector2::operator-(const Vector2& vec) const
    {
        return Vector2(x - vec.x, y - vec.y);
    }

    Vector2 Vector2::operator*(const Vector2& vec) const
    {
        return Vector2(x * vec.x, y * vec.y);
    }

    Vector2 Vector2::operator/(const Vector2& vec) const
    {
        return Vector2(x / vec.x, y / vec.y);
    }

    f32 Vector2::operator[](const i32 idx) const
    {
        if (idx == 0) return x;
        if (idx == 1) return y;

        REI_THROW("Vector2 element idx out of range: " + STRING(idx))
    }

    f32 Vector2::Length() const
    {
        return sqrt(x * x + y * y);
    }

    Vector2 Vector2::One()
    {
        return Vector2(1, 1);
    }

    Vector2 Vector2::Right()
    {
        return Vector2(1, 0);
    }

    Vector2 Vector2::Left()
    {
        return Vector2(-1, 0);
    }

    Vector2 Vector2::Up()
    {
        return Vector2(0, 1);
    }

    Vector2 Vector2::Down()
    {
        return Vector2(0, -1);
    }

    Vector2 Vector2::Max()
    {
        constexpr f32 max = std::numeric_limits<f32>::max();
        return Vector2(max, max);
    }

    Vector2 Vector2::Min()
    {
        constexpr f32 min = std::numeric_limits<f32>::lowest();
        return Vector2(min, min);
    }

    f32 Vector2::Dot(const Vector2& a, const Vector2& b)
    {
        return a.x * b.x + a.y * b.y;
    }

    Vector2 Vector2::Average(const Vector2& a, const Vector2& b)
    {
        return Vector2((a.x + b.x) / 2, (a.y + b.y) / 2);
    }

    f32 Vector2::Length(const Vector2& v)
    {
        return sqrt(v.x * v.x + v.y * v.y);
    }

    Vector2 Vector2::Normalize(const Vector2& v)
    {
        const f32 length = Length(v);
        if (length < 1e-6f)
        {
            return Vector2(0, 0);
        }

        return v / length;
    }

    f32 Vector2::Distance(const Vector2& a, const Vector2& b)
    {
        return Length(a - b);
    }

    Vector2 Vector2::Abs(const Vector2& v)
    {
        return Vector2(abs(v.x), abs(v.y));
    }
}
