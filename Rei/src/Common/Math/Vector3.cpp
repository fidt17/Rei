#include "pch.h"

#include "Vector3.h"

namespace rei::math
{
    Vector3::Vector3(const f32 x, const f32 y, const f32 z)
        : x(x), y(y), z(z)
    {
    }

    Vector3::Vector3(const glm::vec3 vec3)
        : x(vec3.x), y(vec3.y), z(vec3.z)
    {
    }

    Vector3::operator std::string() const
    {
        return "[" + STRING(x) + ", " + STRING(y) + ", " + STRING(z) + "]";
    }

    Vector3 Vector3::operator-() const
    {
        return Vector3(-x, -y, -z);
    }

    Vector3& Vector3::operator+=(const glm::vec<3, float>& vec)
    {
        x += vec.x;
        y += vec.y;
        z += vec.z;

        return *this;
    }

    Vector3& Vector3::operator+=(const Vector3& vec)
    {
        x += vec.x;
        y += vec.y;
        z += vec.z;

        return *this;
    }

    Vector3& Vector3::operator-=(const glm::vec<3, float>& vec)
    {
        x -= vec.x;
        y -= vec.y;
        z -= vec.z;

        return *this;
    }

    Vector3& Vector3::operator-=(const Vector3& vec)
    {
        x -= vec.x;
        y -= vec.y;
        z -= vec.z;

        return *this;
    }

    Vector3& Vector3::operator*=(const float value)
    {
        x *= value;
        y *= value;
        z *= value;

        return *this;
    }

    Vector3& Vector3::operator/=(const float value)
    {
        x /= value;
        y /= value;
        z /= value;

        return *this;
    }

    Vector3 Vector3::operator+(const Vector3& vec) const
    {
        return Vector3(x + vec.x, y + vec.y, z + vec.z);
    }

    Vector3 Vector3::operator-(const Vector3& vec) const
    {
        return Vector3(x - vec.x, y - vec.y, z - vec.z);
    }

    Vector3 Vector3::operator*(const float value) const
    {
        return Vector3(x * value, y * value, z * value);
    }

    Vector3 Vector3::operator/(const float value) const
    {
        return Vector3(x / value, y / value, z / value);
    }

    Vector3 Vector3::operator*(const Vector3& vec) const
    {
        return Vector3(x * vec.x, y * vec.y, z * vec.z);
    }

    Vector3 Vector3::operator/(const Vector3& vec) const
    {
        return Vector3(x / vec.x, y / vec.y, z / vec.z);
    }

    Vector3 Vector3::Transform(const glm::mat4& m) const
    {
        return Vector3(glm::vec3(m * glm::vec4(x, y, z, 1)));
    }

    Vector3 Vector3::Rotate(const glm::quat& q) const
    {
        return Transform(GetRotationMatrix(q));
    }

    f32 Vector3::operator[](const i32 idx) const
    {
        if (idx == 0) return x;
        if (idx == 1) return y;
        if (idx == 2) return z;

        REI_THROW("Vector3 element idx out of range: " + STRING(idx))
    }

    f32 Vector3::Length() const
    {
        return sqrt(x * x + y * y + z * z);
    }

    Vector3 Vector3::One()
    {
        return Vector3(1, 1, 1);
    }

    Vector3 Vector3::Right()
    {
        return Vector3(1, 0, 0);
    }

    Vector3 Vector3::Left()
    {
        return Vector3(-1, 0, 0);
    }

    Vector3 Vector3::Up()
    {
        return Vector3(0, 1, 0);
    }

    Vector3 Vector3::Down()
    {
        return Vector3(0, -1, 0);
    }

    Vector3 Vector3::Forward()
    {
        return Vector3(0, 0, 1);
    }

    Vector3 Vector3::Backwards()
    {
        return Vector3(0, 0, -1);
    }

    Vector3 Vector3::Max()
    {
        constexpr f32 max = std::numeric_limits<f32>::max();
        return Vector3(max, max, max);
    }

    Vector3 Vector3::Min()
    {
        constexpr f32 min = std::numeric_limits<f32>::lowest();
        return Vector3(min, min, min);
    }

    float Vector3::Dot(const Vector3& a, const Vector3& b)
    {
        return a.x * b.x + a.y * b.y + a.z * b.z;
    }

    Vector3 Vector3::Cross(const Vector3& a, const Vector3& b)
    {
        const f32 new_x = a.y * b.z - a.z * b.y;
        const f32 new_y = a.z * b.x - a.x * b.z;
        const f32 new_z = a.x * b.y - a.y * b.x;
        return Vector3(new_x, new_y, new_z);
    }

    Vector3 Vector3::Average(const Vector3& a, const Vector3& b)
    {
        return Vector3((a.x + b.x) / 2, (a.y + b.y) / 2, (a.z + b.z) / 2);
    }

    f32 Vector3::Length(const Vector3& v)
    {
        return sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
    }

    Vector3 Vector3::Normalize(const Vector3& v)
    {
        const f32 length = Length(v);
        if (length < 1e-6f)
        {
            return Vector3(0, 0, 0);
        }

        return v / length;
    }

    f32 Vector3::Distance(const Vector3& a, const Vector3& b)
    {
        return Length(a - b);
    }

    Vector3 Vector3::Abs(const Vector3& v)
    {
        return Vector3(abs(v.x), abs(v.y), abs(v.z));
    }

    Vector3 Vector3::Projection(const Vector3& pointToProject, const Vector3& vectorToProjectOnto)
    {
        return vectorToProjectOnto * Dot(pointToProject, vectorToProjectOnto) / Dot(vectorToProjectOnto, vectorToProjectOnto);
    }
}
