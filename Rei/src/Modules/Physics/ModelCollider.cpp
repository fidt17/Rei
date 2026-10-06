#include "pch.h"
#include "ModelCollider.h"

namespace rei::physics
{
    ColliderType ModelCollider::GetType() const
    {
        return Model;
    }

    bool ModelCollider::IsAvailable() const
    {
        return _model.IsLoaded();
    }

    void ModelCollider::SetModel(const assets::AssetRef<render::Model>& model)
    {
        _model = model;
    }

    bool ModelCollider::Intersect(const math::Ray& ray, const glm::mat4& model, math::Vector3& out_intersectionPoint) const
    {
        using math::Vector3;

        if (!IsAvailable()) return false;

        const auto& meshes = _model->GetMeshes();
        return std::ranges::any_of(meshes, [&](const render::Mesh& m) { return m.BVHRoot.IsRayIntersecting(ray, model, out_intersectionPoint); });
    }
}
