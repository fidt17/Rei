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

bool rei::physics::MeshCollider::Intersect(const math::Ray& ray, const math::Vector3& position, const glm::mat4& model) const
{
    using math::Vector3;

    if (!_model.VerifyIsLoaded()) return false;

    for (const auto& mesh : _model.Asset->GetMeshes())
    {
        const auto& faces = mesh.Faces;

        for (const auto& face : faces)
        {
            constexpr float EPSILON = 1e-6f;

            if (face.Vertices.size() != 3)
            {
#if DEBUG
                LOG_WARNING("Only triangle faces are supported for intersection detection")
#endif
                continue;
            }

            std::array<Vector3, 3> triangle{
                Vector3(glm::vec3(model * glm::vec4(face.Vertices[0].Position, 1))),
                Vector3(glm::vec3(model * glm::vec4(face.Vertices[1].Position, 1))),
                Vector3(glm::vec3(model * glm::vec4(face.Vertices[2].Position, 1))),
            };

            // Möller–Trumbore algorithm
            Vector3 edge1 = triangle[1] - triangle[0];
            Vector3 edge2 = triangle[2] - triangle[0];
            Vector3 h = Vector3::Cross(ray.Direction, edge2);
            const f32 a = Vector3::Dot(edge1, h);

            if (std::fabs(a) < EPSILON)
            {
                continue; // Ray parallel to triangle
            }

            const float f = 1.0f / a;
            Vector3 s = ray.Origin - Vector3(triangle[0]);
            const float u = f * Vector3::Dot(s, h);

            if (u < 0.0f || u > 1.0f)
            {
                continue;
            }

            Vector3 q = Vector3::Cross(s, edge1);
            const float v = f * Vector3::Dot(ray.Direction, q);

            if (v < 0.0f || u + v > 1.0f)
            {
                continue;
            }

            const float t = f * Vector3::Dot(edge2, q);

            if (t > EPSILON)
            {
                return true;
            }
        }
    }

    return false;
}
