#include "pch.h"
#include "support/GeometryTestSupport.h"
#include "Modules/Render/Mesh/MeshBVHNode.h"
#include <algorithm>
#include <random>

using namespace rei::math;
using namespace rei::render;
using namespace rei::tests;

namespace
{
    std::vector<Face> Row(const i32 count)
    {
        std::vector<Face> faces;
        for (i32 i = 0; i < count; ++i) faces.push_back(Triangle({3.0f * i, 0, 5}));
        return faces;
    }

    void Collect(const MeshBVHNode& node, std::vector<f32>& origins, const i32 depth = 0)
    {
        REQUIRE(depth <= 5);
        for (const auto& face : node.Faces)
        {
            REQUIRE(face.Vertices.size() == 3);
            origins.push_back(face.Vertices.front().Position.x);
            for (const auto& vertex : face.Vertices)
            {
                for (i32 axis = 0; axis < 3; ++axis)
                {
                    CHECK(std::isfinite(node.Min[axis]));
                    CHECK(std::isfinite(node.Max[axis]));
                    CHECK(vertex.Position[axis] >= node.Min[axis]);
                    CHECK(vertex.Position[axis] <= node.Max[axis]);
                }
            }
        }
        if (node.Left) Collect(*node.Left, origins, depth + 1);
        if (node.Right) Collect(*node.Right, origins, depth + 1);
    }
}

TEST_CASE("PHY-03 Empty BVH misses without changing output", "[native][bvh][coverage][coverage-next]")
{
    MeshBVHNode node;
    node.BuildBVH(node, {});
    Vector3 point(11, 12, 13);
    CHECK_FALSE(node.IsRayIntersecting(Ray({}, {0, 0, 1}), glm::mat4(1), point));
    CheckVector(point, {11, 12, 13});
}

TEST_CASE("PHY-03 BVH preserves all faces across leaf and split thresholds", "[native][bvh][coverage][coverage-next]")
{
    for (const i32 count : {1, 16, 17, 128})
    {
        CAPTURE(count);
        MeshBVHNode node;
        node.BuildBVH(node, Row(count));
        std::vector<f32> actual;
        Collect(node, actual);
        std::sort(actual.begin(), actual.end());
        REQUIRE(actual.size() == count);
        for (i32 i = 0; i < count; ++i) CHECK(actual[i] == 3.0f * i);
    }
}

TEST_CASE("PHY-03 Flat negative-coordinate geometry has finite containing bounds", "[native][bvh][coverage][coverage-next]")
{
    MeshBVHNode node;
    node.BuildBVH(node, {Triangle({-10, -20, -5})});
    CheckVector(node.Min, {-10, -20, -5.0005f});
    CheckVector(node.Max, {-8, -18, -4.9995f});
    Vector3 point;
    REQUIRE(node.IsRayIntersecting(Ray({-9.5f, -19.5f, -10}, {0, 0, 1}), glm::mat4(1), point));
    CheckVector(point, {-9.5f, -19.5f, -5});
}

TEST_CASE("PHY-03 Seeded BVH rays agree with analytic triangle footprints", "[native][bvh][coverage][coverage-next]")
{
    constexpr u32 SEED = 0xB0A2026;
    MeshBVHNode node;
    node.BuildBVH(node, Row(40));
    std::mt19937 rng(SEED);
    std::uniform_real_distribution<f32> xDistribution(-2, 121);
    std::uniform_real_distribution<f32> yDistribution(-1, 3);
    for (i32 i = 0; i < 600; ++i)
    {
        const f32 x = xDistribution(rng);
        const f32 y = yDistribution(rng);
        const i32 cell = static_cast<i32>(std::floor(x / 3));
        const f32 localX = x - 3 * cell;
        const bool expected = cell >= 0 && cell < 40 && y >= 0 && localX + y <= 2;
        CAPTURE(SEED, i, x, y, expected);
        Vector3 point(11, 12, 13);
        const bool hit = node.IsRayIntersecting(Ray({x, y, 0}, {0, 0, 1}), glm::mat4(1), point);
        CHECK(hit == expected);
        if (hit) CheckVector(point, {x, y, 5});
        else CheckVector(point, {11, 12, 13});
    }
}

TEST_CASE("PHY-03 Transformed split BVH hits every known surface", "[native][bvh][coverage][coverage-next]")
{
    MeshBVHNode node;
    node.BuildBVH(node, Row(17));
    const auto model = GetTransformationMatrix({10, 20, 30}, glm::angleAxis(glm::radians(90.0f), glm::vec3(0, 0, 1)), {2, 3, 4});
    for (i32 i = 0; i < 17; ++i)
    {
        const Vector3 expected(8.5f, 21 + 6.0f * i, 50);
        Vector3 point;
        REQUIRE(node.IsRayIntersecting(Ray({expected.x, expected.y, 0}, {0, 0, 1}), model, point));
        CheckVector(point, expected);
    }
}

TEST_CASE("PHY-03 Coincident centroids terminate and retain duplicate faces", "[native][bvh][coverage][coverage-next]")
{
    MeshBVHNode node;
    node.BuildBVH(node, std::vector<Face>(128, Triangle()));
    std::vector<f32> origins;
    Collect(node, origins);
    REQUIRE(origins.size() == 128);
    Vector3 point;
    REQUIRE(node.IsRayIntersecting(Ray({0.5f, 0.5f, 0}, {0, 0, 1}), glm::mat4(1), point));
    CheckVector(point, {0.5f, 0.5f, 5});
}

TEST_CASE("PHY-03 Leaf to split rebuild exposes only replacement geometry", "[native][bvh][coverage][coverage-next]")
{
    MeshBVHNode node;
    node.BuildBVH(node, {Triangle({-10, 0, 5})});
    node.BuildBVH(node, Row(17));
    Vector3 point;
    CHECK(node.IsRayIntersecting(Ray({0.5f, 0.5f, 0}, {0, 0, 1}), glm::mat4(1), point));
    CheckVector(point, {0.5f, 0.5f, 5});
    std::vector<f32> origins;
    Collect(node, origins);
    CHECK(origins.size() == 17);
}

TEST_CASE("PHY-03 Split to leaf rebuild releases obsolete child ownership", "[native][bvh][coverage][coverage-next]")
{
    MeshBVHNode node;
    node.BuildBVH(node, Row(17));
    REQUIRE(node.Left);
    REQUIRE(node.Right);
    const std::weak_ptr<MeshBVHNode> left = node.Left;
    const std::weak_ptr<MeshBVHNode> right = node.Right;
    node.BuildBVH(node, {Triangle({-10, 0, 5})});
    CHECK(left.expired());
    CHECK(right.expired());
    Vector3 point;
    REQUIRE(node.IsRayIntersecting(Ray({-9.5f, 0.5f, 0}, {0, 0, 1}), glm::mat4(1), point));
    CheckVector(point, {-9.5f, 0.5f, 5});
}

TEST_CASE("PHY-03 Rebuilding empty clears all prior geometry", "[native][bvh][coverage][coverage-next]")
{
    for (const i32 count : {1, 17})
    {
        MeshBVHNode node;
        node.BuildBVH(node, Row(count));
        node.BuildBVH(node, {});
        Vector3 point(11, 12, 13);
        CHECK_FALSE(node.IsRayIntersecting(Ray({0.5f, 0.5f, 0}, {0, 0, 1}), glm::mat4(1), point));
        CheckVector(point, {11, 12, 13});
        CHECK(node.Faces.empty());
        CHECK_FALSE(node.Left);
        CHECK_FALSE(node.Right);
    }
}
