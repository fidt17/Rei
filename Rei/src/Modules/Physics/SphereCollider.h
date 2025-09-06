#pragma once
#include "Collider.h"

namespace rei::physics
{
    class SphereCollider : public Collider
    {
    private:
        SERIALIZABLE_BODY(SphereCollider)
        
        SERIALIZE f32 _radius = 1;

    public:
        REI_API ColliderType GetType() const override;

        REI_API f32 GetRadius() const;
        REI_API void SetRadius(f32 radius);

        REI_API bool Intersect(const math::Ray& ray, const math::Vector3& position, const glm::mat4& model) const override;
    };
}
