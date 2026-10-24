#include "pch.h"
#include "MeshBVHNode.h"
#include "Mesh.h"

void rei::render::MeshBVHNode::BuildBVH(const Mesh& mesh)
{
    std::vector<u32> triangles(mesh.Indices.size() / 3);
    for (u32 i = 0; i < triangles.size(); ++i) triangles[i] = i;
    BuildBVH(mesh, triangles, 0);
}

void rei::render::MeshBVHNode::BuildBVH(const Mesh& mesh, const std::vector<u32>& triangles, const i32 depth)
{
    constexpr i32 MIN_FACES = 16;
    Triangles.clear();
    Left.reset();
    Right.reset();
    Min = {};
    Max = {};
    if (triangles.empty()) return;

    CalculateBoundingBox(mesh, triangles);
    if (triangles.size() <= MIN_FACES || depth >= MAX_DEPTH)
    {
        Triangles = triangles;
        return;
    }

    const math::Vector3 extent = Max - Min;
    i32 axis = 0;
    if (extent.y > extent.x) axis = 1;
    if (extent.z > extent.x && extent.z > extent.y) axis = 2;
    const f32 center = (Min[axis] + Max[axis]) * 0.5f;
    std::vector<u32> leftTriangles, rightTriangles;
    for (const auto triangle : triangles)
    {
        const auto face = mesh.GetFace(triangle);
        f32 faceCenter = 0;
        for (const auto& vertex : face.Vertices) faceCenter += vertex.Position[axis];
        faceCenter /= 3.0f;
        (faceCenter < center ? leftTriangles : rightTriangles).push_back(triangle);
    }
    if (!leftTriangles.empty())
    {
        Left = std::make_shared<MeshBVHNode>();
        Left->BuildBVH(mesh, leftTriangles, depth + 1);
    }
    if (!rightTriangles.empty())
    {
        Right = std::make_shared<MeshBVHNode>();
        Right->BuildBVH(mesh, rightTriangles, depth + 1);
    }
}

bool rei::render::MeshBVHNode::IsRayIntersecting(const Mesh& mesh, const math::Ray& ray, const glm::mat4& model, math::Vector3& out_intersectionPoint) const
{
    const f32 determinant = glm::determinant(glm::mat3(model));
    if (!std::isfinite(determinant) || determinant == 0) return false;

    const auto inverseModel = glm::inverse(model);
    const math::Ray localRay(
        math::Vector3(glm::vec3(inverseModel * glm::vec4(glm::vec3(ray.Origin), 1))),
        math::Vector3(glm::vec3(inverseModel * glm::vec4(glm::vec3(ray.Direction), 0))));
    // Keep direction length: the ray parameter and forward-distance tolerance stay unchanged.
    math::Vector3 localPoint;
    if (!IsLocalRayIntersecting(mesh, localRay, determinant, localPoint)) return false;
    out_intersectionPoint = localPoint.Transform(model);
    return true;
}

bool rei::render::MeshBVHNode::IsLocalRayIntersecting(const Mesh& mesh, const math::Ray& ray, const f32 determinantScale, math::Vector3& out_intersectionPoint) const
{
    if (!math::AxisAlignedBoxRayIntersection(Min, Max, ray)) return false;
    if (!Triangles.empty())
    {
        return std::ranges::any_of(Triangles, [&](const u32 triangle)
        {
            return math::FaceRayIntersection(mesh.GetFace(triangle), ray, out_intersectionPoint, determinantScale);
        });
    }
    return (Left && Left->IsLocalRayIntersecting(mesh, ray, determinantScale, out_intersectionPoint)) ||
           (Right && Right->IsLocalRayIntersecting(mesh, ray, determinantScale, out_intersectionPoint));
}

void rei::render::MeshBVHNode::CalculateBoundingBox(const Mesh& mesh, const std::vector<u32>& triangles)
{
    Min = math::Vector3::Max();
    Max = math::Vector3::Min();

    for (const auto triangle : triangles)
    {
        for (const auto& vertex : mesh.GetFace(triangle).Vertices)
        {
            Min.x = std::min(Min.x, vertex.Position.x);
            Min.y = std::min(Min.y, vertex.Position.y);
            Min.z = std::min(Min.z, vertex.Position.z);
            Max.x = std::max(Max.x, vertex.Position.x);
            Max.y = std::max(Max.y, vertex.Position.y);
            Max.z = std::max(Max.z, vertex.Position.z);
        }
    }

    // Ensure non-zero extents so ray-box tests don't fail for flat meshes.
    constexpr f32 MIN_EXTENT = 0.001f;
    const math::Vector3 extent = Max - Min;

    if (extent.x < MIN_EXTENT)
    {
        const f32 pad = (MIN_EXTENT - extent.x) * 0.5f;
        Min.x -= pad;
        Max.x += pad;
    }

    if (extent.y < MIN_EXTENT)
    {
        const f32 pad = (MIN_EXTENT - extent.y) * 0.5f;
        Min.y -= pad;
        Max.y += pad;
    }

    if (extent.z < MIN_EXTENT)
    {
        const f32 pad = (MIN_EXTENT - extent.z) * 0.5f;
        Min.z -= pad;
        Max.z += pad;
    }
}
