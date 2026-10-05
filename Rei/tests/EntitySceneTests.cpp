#include "pch.h"
#include "support/AssetTestSupport.h"
#include "support/GeometryTestSupport.h"
#include "Modules/Scenes/SceneManager.h"
#include "Modules/Components/ActiveTag.h"

using namespace rei;
using namespace rei::ecs;
using namespace rei::tests;
using nlohmann::json;

namespace
{
    void WriteScenes(TemporaryDirectory& files, const json& first, const json& second = json::array())
    {
        WriteAssetPack(files, {
            {"0", "config", JsonAsset(json{{"Scenes", {{"0", "scene-a"}, {"1", "scene-b"}}}})},
            {"scene-a", "first scene", JsonAsset(json{{"Name", "first"}, {"Entities", first}})},
            {"scene-b", "second scene", JsonAsset(json{{"Name", "second"}, {"Entities", second}})}
        });
    }

    json TransformData(const i32 parent = 0) { return SceneBehaviour(PROBE_TRANSFORM, SerializedField("_parent", parent)); }

    Entity NativeEntity(BehaviourFixture& fixture, const std::string& name)
    {
        const auto entity = fixture.Manager->CreateNewEntity(name);
        fixture.World->RefreshAll();
        return entity;
    }
}

TEST_CASE("ENT-01 CreateNewEntity creates named active root with initialized Transform", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = NativeEntity(fixture, "native root");
        CHECK(fixture.Registry->Get<EntityInfo>(entity).Name == "native root");
        CHECK(fixture.Registry->Has<ActiveTag>(entity));
        CHECK(fixture.Registry->Get<BehaviourCollection>(entity).Behaviours == std::vector<i32>{PROBE_TRANSFORM});
        CHECK(fixture.Registry->Get<StartBehavioursEvent>(entity).Behaviours == std::vector<i32>{PROBE_TRANSFORM});
        CHECK(fixture.Manager->GetRootEntities() == std::vector<Entity>{entity});
        CheckVector(fixture.Registry->Get<Transform>(entity).GetLocalPosition(), {0, 0, 0});
        CheckVector(fixture.Registry->Get<Transform>(entity).GetLocalScale(), {1, 1, 1});
    });
}

TEST_CASE("SCENE-02 First allocated entity ID does not collide with serialized null", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = NativeEntity(fixture, "first");
        CHECK(fixture.Registry->Get<EntityInfo>(entity).Id != 0);
    });
}

TEST_CASE("ENT-01 Consecutive creation without frame refresh still generates unique IDs", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto a = fixture.Manager->CreateNewEntity("a");
        const auto b = fixture.Manager->CreateNewEntity("b");
        const auto c = fixture.Manager->CreateNewEntity("c");
        const auto aId = fixture.Registry->Get<EntityInfo>(a).Id;
        const auto bId = fixture.Registry->Get<EntityInfo>(b).Id;
        const auto cId = fixture.Registry->Get<EntityInfo>(c).Id;
        CHECK(aId != bId);
        CHECK(aId != cId);
        CHECK(bId != cId);
        CHECK(LiveSceneEntities(fixture).size() == 3);
    });
}

TEST_CASE("ENT-01 SceneEntity creation sets native fields before initialization and frame Start", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        fixture.Manager->Create(SceneEntity(SceneObject(7, "serialized", json::array({TransformData(), SceneBehaviour(PROBE_A, SerializedField("Number", 91))}))));
        fixture.World->RefreshAll();
        const auto entity = fixture.Manager->GetBySceneId(7);
        REQUIRE(entity != NULL_ENTITY);
        CHECK(fixture.Registry->Get<ProbeA>(entity).Number == 91);
        CHECK(fixture.Registry->Get<EntityInfo>(entity).Name == "serialized");
        CHECK(fixture.Count(PROBE_A, "Init") == 1);
        CHECK(fixture.Count(PROBE_A, "Start") == 0);
        fixture.Frame();
        CHECK(fixture.Count(PROBE_A, "Start") == 1);
        CHECK(fixture.Count(PROBE_A, "Update") == 1);
    });
}

TEST_CASE("ENT-01 Repeated serialized Behaviour applies final data and initializes once", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        fixture.Manager->Create(SceneEntity(SceneObject(7, "serialized", json::array({TransformData(), SceneBehaviour(PROBE_A, SerializedField("Number", 11)), SceneBehaviour(PROBE_A, SerializedField("Number", 29))}))));
        fixture.World->RefreshAll();
        const auto entity = fixture.Manager->GetBySceneId(7);
        REQUIRE(entity != NULL_ENTITY);
        CHECK(fixture.Registry->Get<ProbeA>(entity).Number == 29);
        CHECK(fixture.Count(PROBE_A, "Init") == 1);
        CHECK(fixture.Registry->Get<BehaviourCollection>(entity).Behaviours == std::vector<i32>{PROBE_TRANSFORM, PROBE_A});
    });
}

TEST_CASE("SCENE-01 Unknown Behaviour creation signals failure to caller", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const SceneEntity entity(SceneObject(7, "invalid", json::array({TransformData(), SceneBehaviour(9999)})));
        CHECK_THROWS(fixture.Manager->Create(entity));
        CHECK(fixture.Events->Calls.empty());
    });
}

TEST_CASE("SCENE-01 Missing Behaviour ID creation signals malformed data", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const SceneEntity entity(SceneObject(7, "invalid", json::array({json::object()})));
        CHECK_THROWS(fixture.Manager->Create(entity));
    });
}

TEST_CASE("ENT-01 Shallow clone preserves scalar nested and primitive collection values", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto source = NativeEntity(fixture, "source");
        fixture.Add(source, PROBE_A);
        auto& value = fixture.Registry->Get<ProbeA>(source);
        value.Number = 93;
        value.Settings.Value = 81;
        value.Values = {8, 9};
        fixture.Registry->Get<Transform>(source).GetLocalPosition() = {1, 2, 3};
        const auto clone = fixture.Manager->Instantiate(source, "copy", false);
        fixture.World->RefreshAll();
        REQUIRE(clone != source);
        CHECK(fixture.Registry->Get<EntityInfo>(clone).Name == "copy");
        CHECK(fixture.Registry->Get<EntityInfo>(clone).Id != fixture.Registry->Get<EntityInfo>(source).Id);
        CHECK(fixture.Registry->Get<ProbeA>(clone).Number == 93);
        CHECK(fixture.Registry->Get<ProbeA>(clone).Settings.Value == 81);
        CHECK(fixture.Registry->Get<ProbeA>(clone).Values == std::vector<i32>{8, 9});
        CheckVector(fixture.Registry->Get<Transform>(clone).GetLocalPosition(), {1, 2, 3});
        fixture.Registry->Get<ProbeA>(clone).Number = 55;
        CHECK(fixture.Registry->Get<ProbeA>(source).Number == 93);
    });
}

TEST_CASE("ENT-01 Clone supports generated custom object collections", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto source = NativeEntity(fixture, "source");
        fixture.Add(source, PROBE_A);
        auto& item = fixture.Registry->Get<ProbeA>(source).Items.emplace_back();
        item.Value = 61;
        item.Label = "item";
        const auto clone = fixture.Manager->Instantiate(source, "copy", false);
        const auto& items = fixture.Registry->Get<ProbeA>(clone).Items;
        REQUIRE(items.size() == 1);
        CHECK(items[0].Value == 61);
        CHECK(items[0].Label == "item");
    });
}

TEST_CASE("ENT-01 Deep clone preserves child order names and independent hierarchy", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto source = NativeEntity(fixture, "root");
        const auto first = NativeEntity(fixture, "first");
        const auto second = NativeEntity(fixture, "second");
        fixture.Registry->Get<Transform>(first).SetParent(source, 0);
        fixture.Registry->Get<Transform>(second).SetParent(source, 1);
        const auto clone = fixture.Manager->Instantiate(source);
        fixture.World->RefreshAll();
        auto children = fixture.Registry->Get<Transform>(clone).GetChildren();
        REQUIRE(children.size() == 2);
        std::ranges::sort(children, [&](Entity a, Entity b) { return fixture.Registry->Get<Transform>(a).GetChildOrder() < fixture.Registry->Get<Transform>(b).GetChildOrder(); });
        CHECK(fixture.Registry->Get<EntityInfo>(children[0]).Name == "first");
        CHECK(fixture.Registry->Get<EntityInfo>(children[1]).Name == "second");
        CHECK(children[0] != first);
        CHECK(children[1] != second);
        CHECK(fixture.Registry->Get<Transform>(children[0]).GetParent() == clone);
        CHECK(fixture.Registry->Get<Transform>(source).GetChildren().size() == 2);
        CHECK(LiveSceneEntities(fixture).size() == 6);
    });
}

TEST_CASE("ENT-01 Clone external ComponentRef keeps original target binding", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto target = fixture.Entity(77);
        const auto source = NativeEntity(fixture, "source");
        fixture.Add(source, PROBE_A);
        fixture.Registry->Get<ProbeA>(source).Target = ComponentRef<Transform>(fixture.Registry, target);
        const auto clone = fixture.Manager->Instantiate(source, "copy", false);
        const auto& ref = fixture.Registry->Get<ProbeA>(clone).Target;
        REQUIRE_FALSE(ref.IsNull());
        CHECK(&ref.Get() == &fixture.Registry->Get<Transform>(target));
    });
}

TEST_CASE("ENT-01 Instantiate rejects NULL and entities without Transform", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        CHECK_THROWS(fixture.Manager->Instantiate(NULL_ENTITY));
        const auto entity = fixture.Entity(7);
        fixture.Registry->Del<Transform>(entity);
        CHECK_THROWS(fixture.Manager->Instantiate(entity));
    });
}

TEST_CASE("ENT-02 Destroy native hierarchy without Behaviour dispatch preserves outsiders", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto root = fixture.Entity(7);
        const auto child = fixture.Entity(8);
        const auto leaf = fixture.Entity(9);
        const auto outside = fixture.Entity(10);
        fixture.Registry->Get<Transform>(child).SetParent(root);
        fixture.Registry->Get<Transform>(leaf).SetParent(child);
        struct Token { std::shared_ptr<i32> Value; };
        std::weak_ptr<i32> lifetime;
        {
            auto value = std::make_shared<i32>(17);
            lifetime = value;
            fixture.Registry->Get<Token>(leaf).Value = value;
        }
        // These native entities have no BehaviourCollection. Engine playmode dispatch is separate.
        fixture.Manager->Destroy(root);
        fixture.World->Refresh(); // RefreshAll updates filters; Refresh drains deferred destruction.
        fixture.World->RefreshAll();
        CHECK_FALSE(fixture.Registry->IsAlive(root));
        CHECK_FALSE(fixture.Registry->IsAlive(child));
        CHECK_FALSE(fixture.Registry->IsAlive(leaf));
        CHECK(fixture.Registry->IsAlive(outside));
        CHECK(lifetime.expired());
        CHECK_NOTHROW(fixture.Manager->Destroy(root));
        CHECK(fixture.Manager->GetRootEntities() == std::vector<Entity>{outside});
    });
}

TEST_CASE("SCENE-01 SceneManager resolves shuffled forward parent and component references", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture([](TemporaryDirectory& files)
        {
            WriteScenes(files, json::array({
                SceneObject(8, "child", json::array({TransformData(7), SceneBehaviour(PROBE_A, SerializedField("Target", SerializedField("SceneEntityId", 7)))})),
                SceneObject(7, "parent", json::array({TransformData()}))
            }));
        });
        scenes::SceneManager manager(fixture.Assets, fixture.Manager);
        manager.LoadScene(0);
        const auto child = fixture.Manager->GetBySceneId(8);
        const auto parent = fixture.Manager->GetBySceneId(7);
        REQUIRE(child != NULL_ENTITY);
        REQUIRE(parent != NULL_ENTITY);
        CHECK(fixture.Registry->Get<Transform>(child).GetParent() == parent);
        const auto& ref = fixture.Registry->Get<ProbeA>(child).Target;
        REQUIRE_FALSE(ref.IsNull());
        CHECK(&ref.Get() == &fixture.Registry->Get<Transform>(parent));
        CHECK(fixture.Manager->GetRootEntities() == std::vector<Entity>{parent});
        fixture.Frame();
        CHECK(fixture.Count(PROBE_A, "Start") == 1);
    });
}

TEST_CASE("SCENE-01 SceneManager rejects duplicate serialized entity IDs", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture([](TemporaryDirectory& files) { WriteScenes(files, json::array({SceneObject(7, "a", json::array({TransformData()})), SceneObject(7, "b", json::array({TransformData()}))})); });
        scenes::SceneManager manager(fixture.Assets, fixture.Manager);
        CHECK_THROWS(manager.LoadScene(0));
        const auto entities = LiveSceneEntities(fixture);
        CHECK(entities.size() <= 1); // No two live entities may share this scene ID.
    });
}

TEST_CASE("SCENE-03 Failed scene dependency prevents successful entity creation", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture([](TemporaryDirectory& files)
        {
            WriteScenes(files, json::array({SceneObject(7, "dependent", json::array({TransformData(), SceneBehaviour(PROBE_A, SerializedField("Asset", SerializedField("Id", "missing")))}))}));
        });
        scenes::SceneManager manager(fixture.Assets, fixture.Manager);
        CHECK_THROWS(manager.LoadScene(0));
        CHECK(LiveSceneEntities(fixture).empty());
    });
}

TEST_CASE("SCENE-03 Switching scene replaces previous scene entities", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture([](TemporaryDirectory& files) { WriteScenes(files, json::array({SceneObject(7, "a", json::array({TransformData()}))}), json::array({SceneObject(8, "b", json::array({TransformData()}))})); });
        scenes::SceneManager manager(fixture.Assets, fixture.Manager);
        manager.LoadScene(0);
        REQUIRE(fixture.Manager->GetBySceneId(7) != NULL_ENTITY);
        manager.LoadScene(1);
        CHECK(fixture.Manager->GetBySceneId(7) == NULL_ENTITY);
        CHECK(fixture.Manager->GetBySceneId(8) != NULL_ENTITY);
        CHECK(LiveSceneEntities(fixture).size() == 1);
    });
}

TEST_CASE("SCENE-03 Loading same scene twice does not duplicate native IDs", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture([](TemporaryDirectory& files) { WriteScenes(files, json::array({SceneObject(7, "a", json::array({TransformData()}))})); });
        scenes::SceneManager manager(fixture.Assets, fixture.Manager);
        manager.LoadScene(0);
        manager.LoadScene(0);
        CHECK(LiveSceneEntities(fixture).size() == 1);
    });
}

TEST_CASE("SCENE-03 Missing build scene ID fails before loading a scene payload", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture([](TemporaryDirectory& files) { WriteScenes(files, json::array()); });
        scenes::SceneManager manager(fixture.Assets, fixture.Manager);
        CHECK_THROWS(manager.LoadScene(99));
        CHECK(LiveSceneEntities(fixture).empty());
        CHECK(fixture.Assets->GetLoadedAssetCount() == 1); // Config only.
    });
}

TEST_CASE("SCENE-03 Empty scene shutdown releases scene and configuration assets", "[native][scene][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture([](TemporaryDirectory& files) { WriteScenes(files, json::array()); });
        scenes::SceneManager manager(fixture.Assets, fixture.Manager);
        manager.LoadScene(0);
        CHECK(fixture.Assets->GetLoadedAssetCount() == 2);
        manager.Shutdown();
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 0);
        CHECK_NOTHROW(manager.Shutdown());
    });
}
