#include "pch.h"
#include "MeshBVHNode.h"

void rei::render::MeshBVHNode::BuildBVH(MeshBVHNode& node, const std::vector<Face>& faces, const int depth)
{
    constexpr int MAX_DEPTH = 5;
    constexpr int MIN_FACES = 16;

    constexpr int X_AXIS = 1;
    constexpr int Y_AXIS = 2;
    constexpr int Z_AXIS = 3;

    if (faces.empty()) return;

    node.CalculateBoundingBox(faces);

    if (faces.size() <= MIN_FACES || depth >= MAX_DEPTH)
    {
        node.Faces = faces;
        return;
    }

    // Split along longest axis
    const math::Vector3 extent = node.Max - node.Min;
    int axis = X_AXIS;
    if (extent.y > extent.x) axis = Y_AXIS;
    if (extent.z > extent.x && extent.z > extent.y) axis = Z_AXIS;

    float center = (node.Min.x + node.Max.x) * 0.5f;
    if (axis == Y_AXIS) center = (node.Min.y + node.Max.y) * 0.5f;
    if (axis == Z_AXIS) center = (node.Min.z + node.Max.z) * 0.5f;

    std::vector<Face> leftFaces, rightFaces;

    for (const auto& face : faces)
    {
        float faceCenter = 0.0f;
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
}
