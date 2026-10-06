#include "pch.h"
#include "support/GeometryTestSupport.h"
#include <algorithm>
#include <array>
#include <limits>

using namespace rei::math;
using namespace rei::tests;

TEST_CASE("MATH-01 Transformation applies scale rotation then translation", "[native][math][coverage][coverage-next]")
{
    const auto rotation = glm::angleAxis(glm::radians(90.0f), glm::vec3(0, 0, 1));
    const auto model = GetTransformationMatrix({10, 20, 30}, rotation, {2, 3, 4});
    CheckVector(Vector3(1, 2, 3).Transform(model), {4, 22, 42});
    CheckVector(Vector3().Transform(model), {10, 20, 30});
    CheckVector(Vector3(1, 0, 0).Rotate(rotation), {0, 1, 0});
}

TEST_CASE("MATH-01 Euler angles use degrees and YXZ order", "[native][math][coverage][coverage-next]")
{
    const auto rotation = GetQuaternion(Vector3(90, 90, 0));
    CheckVector(Vector3::Forward().Rotate(rotation), {0, -1, 0});
    CheckVector(Vector3::Up().Rotate(rotation), {1, 0, 0});
    CheckRotation(GetQuaternion(GetRotationMatrix(rotation)), rotation);
}

TEST_CASE("MATH-01 Euler round trips preserve orientation near gimbal lock", "[native][math][coverage][coverage-next]")
{
    for (const auto angles : std::array<Vector3, 6>{{{0, 0, 0}, {20, 30, -40}, {89.99f, 170, 20}, {90, 15, 45}, {-90, 15, 45}, {120, -230, 300}}})
    {
        CAPTURE(angles.x, angles.y, angles.z);
        const auto rotation = GetQuaternion(angles);
        CheckRotation(GetQuaternion(GetEulerAngles(rotation)), rotation);
    }
}

TEST_CASE("MATH-01 Reference Euler angles remain continuous through yaw wrap", "[native][math][coverage][coverage-next]")
{
    const auto rotation = GetQuaternion(Vector3(0, -179, 0));
    const auto angles = GetEulerAngles(rotation, {0, 179, 0});
    CheckVector(angles, {0, 181, 0}, 1e-3f);
    CheckRotation(GetQuaternion(angles), rotation);
    CheckVector(GetEulerAngles(GetQuaternion(Vector3(10, 20, 30)), {370, 380, 390}), {370, 380, 390}, 1e-3f);
}

TEST_CASE("MATH-01 LookAt aligns positive Z and keeps orthonormal basis", "[native][math][coverage][coverage-next]")
{
    for (const auto direction : std::array<Vector3, 4>{{{0, 0, 3}, {3, 0, 0}, {1, 2, -3}, {0, 0, -2}}})
    {
        const auto q = LookAt(direction, {0, 4, 0});
        CheckVector(Vector3::Forward().Rotate(q), Vector3::Normalize(direction));
        CHECK(glm::length(q) == Catch::Approx(1.0f).margin(1e-4f));
        const auto right = Vector3::Right().Rotate(q);
        const auto up = Vector3::Up().Rotate(q);
        CHECK(Vector3::Dot(right, up) == Catch::Approx(0).margin(1e-4f));
        CheckVector(Vector3::Cross(right, up), Vector3::Normalize(direction));
    }
}

TEST_CASE("MATH-01 LookAt handles parallel and opposite up vectors", "[native][math][coverage][coverage-next]")
{
    for (const auto direction : std::array<Vector3, 3>{{{0, 1, 0}, {0, -1, 0}, {1e-5f, 1, 0}}})
    {
        const auto q = LookAt(direction, {0, 1, 0});
        REQUIRE(std::isfinite(q.w));
        CHECK(glm::length(q) == Catch::Approx(1).margin(1e-4f));
        CheckVector(Vector3::Forward().Rotate(q), Vector3::Normalize(direction));
    }
}

TEST_CASE("PHY-01 Sphere returns analytic entry tangent and exit points", "[native][math][physics][coverage][coverage-next]")
{
    Vector3 point;
    REQUIRE(SphereRayIntersection({}, 1, Ray({0, 0, -3}, {0, 0, 1}), point));
    CheckVector(point, {0, 0, -1});
    REQUIRE(SphereRayIntersection({}, 1, Ray({1, 0, -3}, {0, 0, 1}), point));
    CheckVector(point, {1, 0, 0});
    REQUIRE(SphereRayIntersection({}, 1, Ray({}, {0, 1, 0}), point));
    CheckVector(point, {0, 1, 0});
}

TEST_CASE("PHY-01 Sphere misses preserve output sentinel", "[native][math][physics][coverage][coverage-next]")
{
    for (const auto ray : std::array<Ray, 2>{{Ray({0, 0, -3}, {0, 0, -1}), Ray({2, 0, -3}, {0, 0, 1})}})
    {
        Vector3 point(11, 12, 13);
        CHECK_FALSE(SphereRayIntersection({}, 1, ray, point));
        CheckVector(point, {11, 12, 13});
    }
}

TEST_CASE("PHY-01 Translated sphere and surface origin have analytic hits", "[native][math][physics][coverage][coverage-next]")
{
    Vector3 point;
    REQUIRE(SphereRayIntersection({4, 5, 6}, 2, Ray({4, 5, 0}, {0, 0, 1}), point));
    CheckVector(point, {4, 5, 4});
    REQUIRE(SphereRayIntersection({}, 1, Ray({1, 0, 0}, {1, 0, 0}), point));
    CheckVector(point, {1, 0, 0});
}

TEST_CASE("PHY-02 Box handles axial rays inside outside and behind", "[native][math][physics][coverage][coverage-next]")
{
    const glm::mat4 identity(1);
    CHECK(BoxRayIntersection({2, 2, 2}, Ray({0, 0, -3}, {0, 0, 1}), identity));
    CHECK(BoxRayIntersection({2, 2, 2}, Ray({}, {1, 0, 0}), identity));
    CHECK_FALSE(BoxRayIntersection({2, 2, 2}, Ray({2, 0, -3}, {0, 0, 1}), identity));
    CHECK_FALSE(BoxRayIntersection({2, 2, 2}, Ray({0, 0, -3}, {0, 0, -1}), identity));
}

TEST_CASE("PHY-02 Axial rays on both box boundaries intersect", "[native][math][physics][coverage][coverage-next]")
{
    for (const f32 x : {-1.0f, 1.0f})
    {
        CAPTURE(x);
        CHECK(BoxRayIntersection({2, 2, 2}, Ray({x, 0, -3}, {0, 0, 1}), glm::mat4(1)));
    }
}

TEST_CASE("PHY-02 Box intersection honors translated rotated scaled model", "[native][math][physics][coverage][coverage-next]")
{
    const auto model = GetTransformationMatrix({10, 0, 0}, glm::angleAxis(glm::radians(90.0f), glm::vec3(0, 0, 1)), {4, 1, 2});
    CHECK(BoxRayIntersection({2, 2, 2}, Ray({10, 3, -5}, {0, 0, 1}), model));
    CHECK_FALSE(BoxRayIntersection({2, 2, 2}, Ray({12, 3, -5}, {0, 0, 1}), model));
}

TEST_CASE("PHY-02 Triangle hits interior edges vertices and reversed winding", "[native][math][physics][coverage][coverage-next]")
{
    auto face = Triangle();
    for (const auto target : std::array<Vector3, 3>{{{0.5f, 0.5f, 5}, {1, 1, 5}, {0, 0, 5}}})
    {
        Vector3 point;
        REQUIRE(FaceRayIntersection(face, Ray({target.x, target.y, 0}, {0, 0, 1}), glm::mat4(1), point));
        CheckVector(point, target);
    }
    std::reverse(face.Vertices.begin(), face.Vertices.end());
    Vector3 point;
    REQUIRE(FaceRayIntersection(face, Ray({0.5f, 0.5f, 0}, {0, 0, 1}), glm::mat4(1), point));
    CheckVector(point, {0.5f, 0.5f, 5});
}

TEST_CASE("PHY-02 Triangle misses parallel behind and outside rays", "[native][math][physics][coverage][coverage-next]")
{
    for (const auto ray : std::array<Ray, 4>{{Ray({1.5f, 1.5f, 0}, {0, 0, 1}), Ray({0.5f, 0.5f, 0}, {1, 0, 0}), Ray({0.5f, 0.5f, 6}, {0, 0, 1}), Ray({-1, 0, 0}, {0, 0, 1})}})
    {
        Vector3 point(11, 12, 13);
        CHECK_FALSE(FaceRayIntersection(Triangle(), ray, glm::mat4(1), point));
        CheckVector(point, {11, 12, 13});
    }
}

TEST_CASE("PHY-02 Degenerate and nontriangle faces reject without output mutation", "[native][math][physics][coverage][coverage-next]")
{
    for (const i32 count : {0, 1, 2, 3, 4})
    {
        rei::render::Face face;
        face.Vertices.resize(count); // Coincident vertices also exercise degenerate triangle.
        Vector3 point(11, 12, 13);
        CHECK_FALSE(FaceRayIntersection(face, Ray({}, {0, 0, 1}), glm::mat4(1), point));
        CheckVector(point, {11, 12, 13});
    }
}

TEST_CASE("PHY-02 Triangle hit uses complete model transform", "[native][math][physics][coverage][coverage-next]")
{
    const auto model = GetTransformationMatrix({10, 0, 0}, glm::angleAxis(glm::radians(90.0f), glm::vec3(0, 0, 1)), {2, 3, 1});
    Vector3 point;
    REQUIRE(FaceRayIntersection(Triangle(), Ray({8.5f, 1, 0}, {0, 0, 1}), model, point));
    CheckVector(point, {8.5f, 1, 5});
}

TEST_CASE("MATH-02 Plane hit and misses use signed forward distance", "[native][math][coverage][coverage-next]")
{
    const Plane plane({0, 1, 0}, Vector3(0, 3, 0));
    Vector3 point;
    REQUIRE(PlaneRayIntersection(plane, Ray({}, {0, 1, 0}), point));
    CheckVector(point, {0, 3, 0});
    REQUIRE(PlaneRayIntersection(plane, Ray({0, 3, 0}, {0, 1, 0}), point));
    CheckVector(point, {0, 3, 0});
    for (const auto ray : std::array<Ray, 2>{{Ray({}, {1, 0, 0}), Ray({0, 4, 0}, {0, 1, 0})}})
    {
        point = {11, 12, 13};
        CHECK_FALSE(PlaneRayIntersection(plane, ray, point));
        CheckVector(point, {11, 12, 13});
    }
}

TEST_CASE("MATH-02 Plane through point is invariant to normal magnitude", "[native][math][coverage][coverage-next]")
{
    for (const f32 magnitude : {0.5f, 2.0f, -2.0f})
    {
        CAPTURE(magnitude);
        const Plane plane({0, magnitude, 0}, Vector3(0, 3, 0));
        Vector3 point;
        REQUIRE(PlaneRayIntersection(plane, Ray({}, {0, 1, 0}), point));
        CheckVector(point, {0, 3, 0});
    }
}

TEST_CASE("PHY-02 Local boxes handle parallel boundaries and invalid rays", "[native][math][physics][picking]")
{
    const Vector3 min(-1, -2, 3);
    const Vector3 max(1, 2, 5);
    for (const f32 x : {-1.0f, 0.0f, 1.0f})
    {
        CHECK(AxisAlignedBoxRayIntersection(min, max, Ray({x, 0, 0}, {0, 0, 2})));
        CHECK(AxisAlignedBoxRayIntersection(min, max, Ray({x, 0, 6}, {0, 0, -0.5f})));
    }
    CHECK(AxisAlignedBoxRayIntersection(min, max, Ray({0, 0, 4}, {1, 0, 0})));
    CHECK_FALSE(AxisAlignedBoxRayIntersection(min, max, Ray({2, 0, 0}, {0, 0, 1})));
    CHECK_FALSE(AxisAlignedBoxRayIntersection(min, max, Ray({0, 0, 6}, {0, 0, 1})));
    CHECK_FALSE(AxisAlignedBoxRayIntersection(min, max, Ray({0, 0, 4}, {})));
    CHECK_FALSE(AxisAlignedBoxRayIntersection(max, min, Ray({}, {0, 0, 1})));
    const f32 nan = std::numeric_limits<f32>::quiet_NaN();
    const f32 infinity = std::numeric_limits<f32>::infinity();
    CHECK_FALSE(AxisAlignedBoxRayIntersection(min, max, Ray({nan, 0, 0}, {0, 0, 1})));
    CHECK_FALSE(AxisAlignedBoxRayIntersection(min, max, Ray({}, {infinity, 0, 1})));
}

TEST_CASE("PHY-02 Local triangle keeps non-unit ray distance and determinant tolerance", "[native][math][physics][picking]")
{
    Vector3 point(91, 92, 93);
    REQUIRE(FaceRayIntersection(Triangle(), Ray({0.5f, 0.5f, 0}, {0, 0, 0.5f}), point));
    CheckVector(point, {0.5f, 0.5f, 5});
    // Determinant is 4e-8 locally; scaling the world triangle makes it a valid hit.
    REQUIRE(FaceRayIntersection(Triangle(), Ray({0.5f, 0.5f, 0}, {0, 0, 1e-8f}), point, 100));
    CheckVector(point, {0.5f, 0.5f, 5});
    point = {91, 92, 93};
    CHECK_FALSE(FaceRayIntersection(Triangle(), Ray({0.5f, 0.5f, 0}, {0, 0, 1}), point, 1e-8f));
    CheckVector(point, {91, 92, 93});
}
