#pragma once
#include "Collider.h"
#include "Modules/Render/Model/Model.h"

namespace rei::physics
{
    class MeshCollider : public Collider
    {
    private:
        SERIALIZABLE_BODY(MeshCollider)

        assets::AssetRef<render::Model> _model;

    public:
        REI_API ColliderType GetType() const override;

        REI_API void SetModel(const assets::AssetRef<render::Model>& model);

        REI_API bool Intersect(const math::Ray& ray, const glm::mat4& model) const override;
    };
}
