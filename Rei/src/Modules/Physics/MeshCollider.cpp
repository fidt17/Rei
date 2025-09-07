#include "pch.h"
#include "MeshCollider.h"

rei::physics::ColliderType rei::physics::MeshCollider::GetType() const
{
    return Mesh;
}

void rei::physics::MeshCollider::SetModel(const assets::AssetRef<render::Model>& model)
{
    _model = model;
}

bool rei::physics::MeshCollider::Intersect(const math::Ray& ray, const glm::mat4& model) const
{
    using math::Vector3;

    if (!_model.VerifyIsLoaded()) return false;

    const auto& meshes = _model.Asset->GetMeshes();
    return std::ranges::any_of(meshes, [&](const render::Mesh& m) { return  m.BVHRoot.IsRayIntersecting(ray, model); });
}
