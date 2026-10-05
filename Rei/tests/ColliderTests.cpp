#include "pch.h"
#include "support/GeometryTestSupport.h"
#include "Modules/Physics/SphereCollider.h"
#include "Modules/Physics/ModelCollider.h"
#include "Modules/Assets/Registry/AssetRegistry.h"

using namespace rei::math;
using namespace rei::physics;
using namespace rei::tests;

TEST_CASE("PHY-01 SphereCollider radius changes observable surface", "[native][physics][coverage][coverage-next]")
{
    SphereCollider sphere;
    CHECK(sphere.GetType() == ColliderType::Sphere);
    CHECK(sphere.GetRadius() == 1);
    Vector3 point;
    REQUIRE(sphere.Intersect(Ray({0, 0, -5}, {0, 0, 1}), glm::mat4(1), point));
    CheckVector(point, {0, 0, -1});
    sphere.SetRadius(2);
    REQUIRE(sphere.Intersect(Ray({0, 0, -5}, {0, 0, 1}), glm::mat4(1), point));
    CheckVector(point, {0, 0, -2});
}

TEST_CASE("PHY-01 SphereCollider translates center and preserves misses", "[native][physics][coverage][coverage-next]")
{
    SphereCollider sphere;
    const auto model = glm::translate(glm::mat4(1), glm::vec3(4, 5, 6));
    Vector3 point;
    REQUIRE(sphere.Intersect(Ray({4, 5, 0}, {0, 0, 1}), model, point));
    CheckVector(point, {4, 5, 5});
    point = {11, 12, 13};
    CHECK_FALSE(sphere.Intersect(Ray({}, {0, 0, 1}), model, point));
    CheckVector(point, {11, 12, 13});
}

TEST_CASE("PHY-04 Unbound and missing model colliders preserve misses", "[native][physics][coverage][coverage-next]")
{
    ModelCollider collider;
    CHECK(collider.GetType() == ColliderType::Model);
    Vector3 point(11, 12, 13);
    CHECK_FALSE(collider.Intersect(Ray({}, {0, 0, 1}), glm::mat4(1), point));
    collider.SetModel(rei::assets::AssetRef<rei::render::Model>("missing"));
    CHECK_FALSE(collider.Intersect(Ray({}, {0, 0, 1}), glm::mat4(1), point));
    CheckVector(point, {11, 12, 13});
}

TEST_CASE("PHY-04 Empty loaded model survives release and reload binding", "[native][physics][coverage][coverage-next]")
{
    // Empty model constructor performs no mesh PostLoad and needs no GL context.
    rei::assets::AssetRegistry registry;
    rei::assets::AssetRef<rei::render::Model> ref("empty");
    const auto load = [&]
    {
        std::vector<rei::render::Mesh> meshes;
        registry.CreateAssetRecord(ref, "empty", new rei::render::Model("empty", meshes), 1, rei::assets::AssetState::Loaded);
        registry.SetRefCount(ref.Id, 1);
    };
    load();
    ModelCollider collider;
    collider.SetModel(ref);
    Vector3 point(11, 12, 13);
    REQUIRE(ref.IsLoaded());
    CHECK_FALSE(collider.Intersect(Ray({}, {0, 0, 1}), glm::mat4(1), point));
    registry.ReleaseAssetWithId(ref.Id);
    CHECK_FALSE(ref.IsLoaded());
    CHECK_FALSE(collider.Intersect(Ray({}, {0, 0, 1}), glm::mat4(1), point));
    load();
    REQUIRE(ref.IsLoaded());
    CHECK_FALSE(collider.Intersect(Ray({}, {0, 0, 1}), glm::mat4(1), point));
    CheckVector(point, {11, 12, 13});
}
