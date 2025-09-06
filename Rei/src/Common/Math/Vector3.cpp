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
        return Vector3( x + vec.x, y + vec.y, z + vec.z );
    }

    Vector3 Vector3::operator-(const Vector3& vec) const
    {
        return Vector3( x - vec.x, y - vec.y, z - vec.z );
    }

    Vector3 Vector3::operator*(const float value) const
    {
        return Vector3( x * value, y * value, z * value );
    }

    Vector3 Vector3::operator/(const float value) const
    {
        return Vector3( x / value, y / value, z / value );
    }

    f32 Vector3::Length() const
    {
        return sqrt(x * x + y * y + z * z);
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
}
