#pragma once

namespace rei::render
{
    class Mesh;

    class MeshBVHNode
    {
    public:
        static constexpr i32 MAX_DEPTH = 5;

        math::Vector3 Min{};
        math::Vector3 Max{};
        std::shared_ptr<MeshBVHNode> Left = nullptr;
        std::shared_ptr<MeshBVHNode> Right = nullptr;
        std::vector<u32> Triangles{};

        void BuildBVH(const Mesh& mesh);

        bool IsRayIntersecting(const Mesh& mesh, const math::Ray& ray, const glm::mat4& model, math::Vector3& out_intersectionPoint) const;

    private:
        void BuildBVH(const Mesh& mesh, const std::vector<u32>& triangles, i32 depth);
        bool IsLocalRayIntersecting(const Mesh& mesh, const math::Ray& ray, f32 determinantScale, math::Vector3& out_intersectionPoint) const;
        void CalculateBoundingBox(const Mesh& mesh, const std::vector<u32>& triangles);
    };
}
