#include "pch.h"
#include "support/GeometryTestSupport.h"
#include "Modules/Physics/SphereCollider.h"
#include <array>

using namespace rei::math;
using namespace rei::tests;

namespace
{
    constexpr std::array<f32, 3> DIRECTION_SCALES = {0.5f, 2.0f, 8.0f};

    // Positive reparameterization O + t * (kD) describes the same half-line.
    // Non-unit support is a proposed public contract: Ray stores the supplied
    // vector without normalization and its header declares no unit precondition.
    // Passing characterization tests document existing behavior separately.
    void CheckSphereSurface(const Vector3& point, const Vector3& center, const f32 radius)
    {
        const auto offset = point - center;
        const f32 distanceSquared = offset.x * offset.x + offset.y * offset.y + offset.z * offset.z;
        CHECK(distanceSquared == Catch::Approx(radius * radius).margin(1e-4f));
    }
}

TEST_CASE("PHY-01 Proposed nonunit sphere rays preserve analytic entry point", "[native][physics][coverage][coverage-remaining][proposed-contract][isolated]")
{
    Isolated([]
    {
        for (const auto scale : DIRECTION_SCALES)
        {
            CAPTURE(scale);
            Vector3 point;
            REQUIRE(SphereRayIntersection({4, 5, 6}, 2, Ray({4, 5, 0}, {0, 0, scale}), point));
            CheckVector(point, {4, 5, 4});
            CheckSphereSurface(point, {4, 5, 6}, 2);
        }
    });
}

TEST_CASE("PHY-01 Proposed nonunit sphere tangent and inside exit stay on surface", "[native][physics][coverage][coverage-remaining][proposed-contract][isolated]")
{
    Isolated([]
    {
        for (const auto scale : DIRECTION_SCALES)
        {
            CAPTURE(scale);
            Vector3 point;
            REQUIRE(SphereRayIntersection({}, 2, Ray({2, 0, -4}, {0, 0, scale}), point));
            CheckVector(point, {2, 0, 0});
            CheckSphereSurface(point, {}, 2);
            REQUIRE(SphereRayIntersection({}, 2, Ray({}, {0, scale, 0}), point));
            CheckVector(point, {0, 2, 0});
            CheckSphereSurface(point, {}, 2);
        }
    });
}

TEST_CASE("PHY-01 Proposed nonunit sphere misses preserve output sentinel", "[native][physics][coverage][coverage-remaining][proposed-contract][isolated]")
{
    Isolated([]
    {
        for (const auto scale : DIRECTION_SCALES)
        {
            CAPTURE(scale);
            Vector3 point(91, 92, 93);
            CHECK_FALSE(SphereRayIntersection({}, 1, Ray({2, 0, -3}, {0, 0, scale}), point));
            CheckVector(point, {91, 92, 93});
            CHECK_FALSE(SphereRayIntersection({}, 1, Ray({0, 0, -3}, {0, 0, -scale}), point));
            CheckVector(point, {91, 92, 93});
        }
    });
}

TEST_CASE("PHY-02 Nonunit plane rays retain analytic oblique hit and misses", "[native][physics][coverage][coverage-remaining][characterization][isolated]")
{
    Isolated([]
    {
        const Plane plane({0, 1, 0}, Vector3(0, 3, 0));
        for (const auto scale : DIRECTION_SCALES)
        {
            CAPTURE(scale);
            Vector3 point;
            // y: -3 + 3*k*t = 3, so x=5 and z=10 for every k.
            REQUIRE(PlaneRayIntersection(plane, Ray({1, -3, 2}, {2 * scale, 3 * scale, 4 * scale}), point));
            CheckVector(point, {5, 3, 10});
            point = {91, 92, 93};
            CHECK_FALSE(PlaneRayIntersection(plane, Ray({1, -3, 2}, {-2 * scale, -3 * scale, -4 * scale}), point));
            CheckVector(point, {91, 92, 93});
            CHECK_FALSE(PlaneRayIntersection(plane, Ray({1, -3, 2}, {2 * scale, 0, 4 * scale}), point));
            CheckVector(point, {91, 92, 93});
        }
    });
}

TEST_CASE("PHY-02 Nonunit face rays retain analytic oblique triangle hit", "[native][physics][coverage][coverage-remaining][characterization][isolated]")
{
    Isolated([]
    {
        for (const auto scale : DIRECTION_SCALES)
        {
            CAPTURE(scale);
            Vector3 point;
            // At z=5, t=1/k and position=(0.5,0.5,5), inside footprint.
            REQUIRE(FaceRayIntersection(Triangle(), Ray({-1.5f, -0.5f, 0}, {2 * scale, scale, 5 * scale}), glm::mat4(1), point));
            CheckVector(point, {0.5f, 0.5f, 5});
            point = {91, 92, 93};
            CHECK_FALSE(FaceRayIntersection(Triangle(), Ray({-1.5f, -0.5f, 0}, {2 * scale, scale, -5 * scale}), glm::mat4(1), point));
            CheckVector(point, {91, 92, 93});
        }
    });
}

TEST_CASE("PHY-02 Proposed fast face ray uses world distance rather than parameter epsilon", "[native][physics][coverage][coverage-remaining][proposed-contract][isolated]")
{
    Isolated([]
    {
        Vector3 point;
        // Intersection lies five world units ahead. Large direction magnitude
        // makes t small but does not move geometry near ray origin.
        REQUIRE(FaceRayIntersection(Triangle(), Ray({0.5f, 0.5f, 0}, {0, 0, 1e7f}), glm::mat4(1), point));
        CheckVector(point, {0.5f, 0.5f, 5});
    });
}

TEST_CASE("PHY-02 Nonunit box rays preserve analytic slab membership", "[native][physics][coverage][coverage-remaining][characterization][isolated]")
{
    Isolated([]
    {
        for (const auto scale : DIRECTION_SCALES)
        {
            CAPTURE(scale);
            // For origin (-3,0,0), D=(2,.25,.5), x-slab interval
            // [1/k,2/k] overlaps y and z intervals: hit. Raising y to 3
            // with nonnegative D.y prevents entry: miss. Reverse ray is behind.
            CHECK(BoxRayIntersection({2, 2, 2}, Ray({-3, 0, 0}, {2 * scale, 0.25f * scale, 0.5f * scale}), glm::mat4(1)));
            CHECK_FALSE(BoxRayIntersection({2, 2, 2}, Ray({-3, 3, 0}, {2 * scale, 0.25f * scale, 0.5f * scale}), glm::mat4(1)));
            CHECK_FALSE(BoxRayIntersection({2, 2, 2}, Ray({-3, 0, 0}, {-2 * scale, -0.25f * scale, -0.5f * scale}), glm::mat4(1)));
        }
    });
}

TEST_CASE("PHY-02 Nonunit box rays retain transformed slab geometry", "[native][physics][coverage][coverage-remaining][characterization][isolated]")
{
    Isolated([]
    {
        const auto model = GetTransformationMatrix({10, 2, -3}, GetQuaternion(Vector3(0, 0, 90)), {2, 3, 4});
        for (const auto scale : DIRECTION_SCALES)
        {
            CAPTURE(scale);
            // Local O=(-3,0,0), D=(2,.25,.5) becomes the values below.
            CHECK(BoxRayIntersection({2, 2, 2}, Ray({10, -4, -3}, {-0.75f * scale, 4 * scale, 2 * scale}), model));
            // Local y=3 maps to world x=1 and cannot enter y slab.
            CHECK_FALSE(BoxRayIntersection({2, 2, 2}, Ray({1, -4, -3}, {-0.75f * scale, 4 * scale, 2 * scale}), model));
        }
    });
}

TEST_CASE("PHY-01 Proposed zero direction rejects degenerate ray without changing output", "[native][physics][coverage][coverage-remaining][proposed-contract][isolated]")
{
    Isolated([]
    {
        const Ray zero({}, {});
        Vector3 point(91, 92, 93);
        CHECK_FALSE(SphereRayIntersection({}, 1, zero, point));
        CheckVector(point, {91, 92, 93});
        CHECK_FALSE(PlaneRayIntersection(Plane({0, 1, 0}, Vector3(0, 3, 0)), zero, point));
        CheckVector(point, {91, 92, 93});
        CHECK_FALSE(FaceRayIntersection(Triangle(), zero, glm::mat4(1), point));
        CheckVector(point, {91, 92, 93});
        CHECK_FALSE(BoxRayIntersection({2, 2, 2}, zero, glm::mat4(1)));
    });
}

TEST_CASE("PHY-04 Characterization SphereCollider radius is currently independent of model scale", "[native][physics][coverage][coverage-remaining][characterization][isolated]")
{
    Isolated([]
    {
        rei::physics::SphereCollider collider;
        collider.SetRadius(2);
        for (const auto modelScale : std::array<Vector3, 3>{{{3, 3, 3}, {-3, -3, -3}, {3, 2, 0.5f}}})
        {
            const auto model = GetTransformationMatrix({10, 20, 30}, glm::quat(1, 0, 0, 0), modelScale);
            Vector3 point;
            REQUIRE(collider.Intersect(Ray({10, 20, 0}, {0, 0, 1}), model, point));
            CheckVector(point, {10, 20, 28});
            CHECK(collider.GetRadius() == 2);
        }
    });
}

TEST_CASE("PHY-04 Proposed local SphereCollider radius follows absolute uniform model scale", "[native][physics][coverage][coverage-remaining][proposed-contract][isolated]")
{
    Isolated([]
    {
        // Alternative product policy to characterization above, not a claim
        // about a documented existing contract. Decide radius local/world
        // semantics before treating this red test as an approved bug fix.
        rei::physics::SphereCollider collider;
        collider.SetRadius(2);
        for (const f32 scale : {0.5f, 3.0f, -3.0f})
        {
            CAPTURE(scale);
            const auto model = GetTransformationMatrix({10, 20, 30}, glm::quat(1, 0, 0, 0), {scale, scale, scale});
            Vector3 point;
            REQUIRE(collider.Intersect(Ray({10, 20, 0}, {0, 0, 1}), model, point));
            CheckVector(point, {10, 20, 30 - 2 * std::abs(scale)});
            CHECK(collider.GetRadius() == 2); // Local serialized value stays fixed.
        }
    });
}
