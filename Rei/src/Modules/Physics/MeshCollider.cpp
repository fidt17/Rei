#include "pch.h"
#include "MeshCollider.h"

#include "assimp/port/AndroidJNI/AndroidJNIIOSystem.h"
#include "assimp/port/AndroidJNI/AndroidJNIIOSystem.h"
#include "assimp/port/AndroidJNI/AndroidJNIIOSystem.h"
#include "assimp/port/AndroidJNI/AndroidJNIIOSystem.h"
#include "assimp/port/AndroidJNI/AndroidJNIIOSystem.h"
#include "assimp/port/AndroidJNI/AndroidJNIIOSystem.h"
#include "assimp/port/AndroidJNI/AndroidJNIIOSystem.h"
#include "assimp/port/AndroidJNI/AndroidJNIIOSystem.h"

rei::physics::ColliderType rei::physics::MeshCollider::GetType() const
{
    return Mesh;
}

void rei::physics::MeshCollider::SetModel(const assets::AssetRef<render::Model>& model)
{
    _model = model;
}

bool rei::physics::MeshCollider::IntersectBVH(const math::Ray& ray, const math::Vector3& position, const glm::mat4& model,
                                              const render::MeshBVHNode& node) const
{
    using math::Vector3;

    if (!BoxRayIntersection(node.Min, ray, model)) return false;

    if (!node.Faces.empty())
    {
        for (const auto& face : node.Faces)
        {
            if (FaceRayIntersection(face, ray, model))
            {
                return true;
            }
        }

        return false;
    }

    if (node.Left)
    {
        if (IntersectBVH(ray, position, model, *node.Left))
        {
            return true;
        }
    }

    if (node.Right)
    {
        if (IntersectBVH(ray, position, model, *node.Right))
        {
            return true;
        }
    }

    return false;
}

bool rei::physics::MeshCollider::Intersect(const math::Ray& ray, const math::Vector3& position, const glm::mat4& model) const
{
    using math::Vector3;

    if (!_model.VerifyIsLoaded()) return false;

    for (const auto& mesh : _model.Asset->GetMeshes())
    {
        /*
        for (const auto & face : mesh.Faces)
        {
            if (math::FaceRayIntersection(face, ray, model))
            {
                return true;
            }
        }
        */
        if (IntersectBVH(ray, position, model, mesh.BVHRoot))
        {
            return true;
        }
    }
    return false;
}
