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
}
