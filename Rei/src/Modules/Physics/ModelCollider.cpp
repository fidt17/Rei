#include "pch.h"
#include "ModelCollider.h"

namespace rei::physics
{
    ColliderType ModelCollider::GetType() const
    {
        return Model;
    }

    void ModelCollider::SetModel(const assets::AssetRef<render::Model>& model)
    {
        _model = model;
    }

    bool ModelCollider::Intersect(const math::Ray& ray, const glm::mat4& model, math::Vector3& out_intersectionPoint) const
    {
        using math::Vector3;

        if (!_model.IsLoaded()) return false;

        const auto& meshes = _model->GetMeshes();
        return std::ranges::any_of(meshes, [&](const render::Mesh& m) { return m.BVHRoot.IsRayIntersecting(ray, model, out_intersectionPoint); });
    }
}
