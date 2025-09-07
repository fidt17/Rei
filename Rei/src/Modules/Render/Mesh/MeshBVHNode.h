#pragma once
#include "Face.h"

namespace rei::render
{
    class MeshBVHNode
    {
    public:
        math::Vector3 Min{};
        math::Vector3 Max{};
        std::shared_ptr<MeshBVHNode> Left = nullptr;
        std::shared_ptr<MeshBVHNode> Right = nullptr;
        std::vector<Face> Faces{};

    public:
        void BuildBVH(MeshBVHNode& node, const std::vector<Face>& faces, int depth = 0);

        bool IsRayIntersecting(const math::Ray& ray, const glm::mat4& model) const;

    private:
        void CalculateBoundingBox(const std::vector<Face>& faces);
    };
}
