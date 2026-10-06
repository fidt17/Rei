#include "pch.h"
#include "MeshBVHNode.h"

void rei::render::MeshBVHNode::BuildBVH(MeshBVHNode& node, const std::vector<Face>& faces, const i32 depth)
{
    constexpr i32 MAX_DEPTH = 5;
    constexpr i32 MIN_FACES = 16;

    constexpr i32 X_AXIS = 1;
    constexpr i32 Y_AXIS = 2;
    constexpr i32 Z_AXIS = 3;

    if (faces.empty()) return;

    node.CalculateBoundingBox(faces);

    if (faces.size() <= MIN_FACES || depth >= MAX_DEPTH)
    {
        node.Faces = faces;
        return;
    }

    // Split along longest axis
    const math::Vector3 extent = node.Max - node.Min;
    i32 axis = X_AXIS;
    if (extent.y > extent.x) axis = Y_AXIS;
    if (extent.z > extent.x && extent.z > extent.y) axis = Z_AXIS;

    f32 center = (node.Min.x + node.Max.x) * 0.5f;
    if (axis == Y_AXIS) center = (node.Min.y + node.Max.y) * 0.5f;
    if (axis == Z_AXIS) center = (node.Min.z + node.Max.z) * 0.5f;

    std::vector<Face> leftFaces, rightFaces;

    for (const auto& face : faces)
    {
        f32 faceCenter = 0.0f;
        for (const auto& vert : face.Vertices)
        {
            if (axis == X_AXIS) faceCenter += vert.Position.x;
            else if (axis == Y_AXIS) faceCenter += vert.Position.y;
            else faceCenter += vert.Position.z;
        }
        faceCenter /= 3.0f;

        if (faceCenter < center)
        {
            leftFaces.push_back(face);
        }
        else
        {
            rightFaces.push_back(face);
        }
    }

    if (!leftFaces.empty())
    {
        node.Left = std::make_shared<MeshBVHNode>();
        BuildBVH(*node.Left, leftFaces, depth + 1);
    }

    if (!rightFaces.empty())
    {
        node.Right = std::make_shared<MeshBVHNode>();
        BuildBVH(*node.Right, rightFaces, depth + 1);
    }
}

bool rei::render::MeshBVHNode::IsRayIntersecting(const math::Ray& ray, const glm::mat4& model, math::Vector3& out_intersectionPoint) const
{
    const f32 determinant = glm::determinant(glm::mat3(model));
    if (!std::isfinite(determinant) || determinant == 0) return false;

    const auto inverseModel = glm::inverse(model);
    const math::Ray localRay(
        math::Vector3(glm::vec3(inverseModel * glm::vec4(glm::vec3(ray.Origin), 1))),
        math::Vector3(glm::vec3(inverseModel * glm::vec4(glm::vec3(ray.Direction), 0))));
    // Keep direction length: the ray parameter and forward-distance tolerance stay unchanged.
    math::Vector3 localPoint;
    if (!IsLocalRayIntersecting(localRay, determinant, localPoint)) return false;
    out_intersectionPoint = localPoint.Transform(model);
    return true;
}

bool rei::render::MeshBVHNode::IsLocalRayIntersecting(const math::Ray& ray, const f32 determinantScale, math::Vector3& out_intersectionPoint) const
{
    if (!math::AxisAlignedBoxRayIntersection(Min, Max, ray)) return false;
    if (!Faces.empty())
    {
        return std::ranges::any_of(Faces, [&](const auto& face)
        {
            return math::FaceRayIntersection(face, ray, out_intersectionPoint, determinantScale);
        });
    }
    return (Left && Left->IsLocalRayIntersecting(ray, determinantScale, out_intersectionPoint)) ||
           (Right && Right->IsLocalRayIntersecting(ray, determinantScale, out_intersectionPoint));
}

void rei::render::MeshBVHNode::CalculateBoundingBox(const std::vector<Face>& faces)
{
    Min = math::Vector3::Max();
    Max = math::Vector3::Min();

    for (const auto& face : faces)
    {
        for (const auto& vertex : face.Vertices)
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
