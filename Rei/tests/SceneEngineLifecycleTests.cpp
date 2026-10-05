#include "pch.h"
#include "support/NativeEngineFixture.h"
#include "Modules/Scenes/SceneManager.h"
#include "Api/AssetApi.h"

using namespace rei;
using namespace rei::tests;
using namespace rei::internal::engine;

namespace
{
    void NonemptyScene(TemporaryDirectory& files)
    {
        PrepareEngineResources(files, "scene-lifecycle", nlohmann::json::array({
            SceneObject(8, "child", nlohmann::json::array({SceneBehaviour(PROBE_TRANSFORM, SerializedField("_parent", 7)), SceneBehaviour(PROBE_A)})),
            SceneObject(7, "parent", nlohmann::json::array({SceneBehaviour(PROBE_TRANSFORM), SceneBehaviour(PROBE_A)}))
        }));
    }
}

TEST_CASE("SCENE-02 Actual engine shutdown disposes nonempty scene child before parent", "[native][engine-integration][scene][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture fixture(PlayMode, "scene", NonemptyScene);
        fixture.Start();
        ecs::Entity parent = ecs::NULL_ENTITY, child = ecs::NULL_ENTITY;
        std::shared_ptr<ecs::EcsRegistry> registry;
        std::vector<ecs::Entity> disposed;
        fixture.OnEngineThread([&]
        {
            registry = GetInternalWorld()->GetRegistry();
            parent = GetEntityManager().GetBySceneId(7);
            child = GetEntityManager().GetBySceneId(8);
            fixture.Resources.Events->Action = [&](i32 id, const std::string& stage, const ecs::Entity entity)
            {
                if (id == PROBE_A && stage == "Dispose") disposed.push_back(entity);
            };
        });
        REQUIRE(parent != ecs::NULL_ENTITY);
        REQUIRE(child != ecs::NULL_ENTITY);
        fixture.Stop();
        fixture.Resources.Events->Action = {};
        CHECK_FALSE(registry->IsAlive(parent));
        CHECK_FALSE(registry->IsAlive(child));
        REQUIRE(disposed == std::vector<ecs::Entity>{child, parent});
        CHECK(GetAssetManager().GetLoadedAssetCount() == 0);
    }, 20000, 1024);
}

TEST_CASE("SCENE-02 Native SceneManager unload and reload resolve fresh parent handles", "[native][engine-integration][scene][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture fixture(PlayMode, "scene", NonemptyScene);
        fixture.Start();
        bool oldParentAlive = true, oldChildAlive = true, newParentAlive = false, newChildAlive = false, parentResolved = false;
        i32 disposed = -1;
        fixture.OnEngineThread([&]
        {
            const auto world = GetInternalWorld();
            const auto registry = world->GetRegistry();
            const auto oldParent = GetEntityManager().GetBySceneId(7);
            const auto oldChild = GetEntityManager().GetBySceneId(8);
            auto assets = std::shared_ptr<assets::AssetManager>(&GetAssetManager(), [](auto*) {});
            auto entities = std::shared_ptr<EntityManager>(&GetEntityManager(), [](auto*) {});
            scenes::SceneManager manager(assets, entities);
            manager.UnloadCurrentScene();
            oldParentAlive = registry->IsAlive(oldParent);
            oldChildAlive = registry->IsAlive(oldChild);
            disposed = fixture.Resources.Count(PROBE_A, "Dispose");
            manager.UnloadCurrentScene();
            manager.LoadScene(0);
            const auto newParent = GetEntityManager().GetBySceneId(7);
            const auto newChild = GetEntityManager().GetBySceneId(8);
            newParentAlive = registry->IsAlive(newParent);
            newChildAlive = registry->IsAlive(newChild);
            parentResolved = registry->Get<Transform>(newChild).GetParent() == newParent;
            manager.Shutdown();
        });
        CHECK_FALSE(oldParentAlive);
        CHECK_FALSE(oldChildAlive);
        CHECK(newParentAlive);
        CHECK(newChildAlive);
        CHECK(parentResolved);
        CHECK(disposed == 2);
        fixture.Stop();
    }, 20000, 1024);
}

TEST_CASE("ENT-01 Clone reference policy retains original internal and external targets", "[native][scene][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto source = fixture.Manager->CreateNewEntity("source");
        const auto child = fixture.Manager->CreateNewEntity("child");
        const auto external = fixture.Manager->CreateNewEntity("external");
        fixture.Registry->Get<Transform>(child).SetParent(source);
        fixture.Add(source, PROBE_A);
        fixture.Registry->Get<ProbeA>(source).Target = ecs::ComponentRef<Transform>(fixture.Registry, child);
        const auto clone = fixture.Manager->Instantiate(source, "clone", true);
        fixture.World->Refresh();
        REQUIRE(fixture.Registry->Get<Transform>(clone).GetChildren().size() == 1);
        const auto clonedChild = fixture.Registry->Get<Transform>(clone).GetChildren()[0];
        const auto& clonedTarget = fixture.Registry->Get<ProbeA>(clone).Target;
        REQUIRE_FALSE(clonedTarget.IsNull());
        CHECK(&clonedTarget.Get() == &fixture.Registry->Get<Transform>(child));
        CHECK(&clonedTarget.Get() != &fixture.Registry->Get<Transform>(clonedChild));
        fixture.Registry->Get<ProbeA>(source).Target = ecs::ComponentRef<Transform>(fixture.Registry, external);
        const auto externalClone = fixture.Manager->Instantiate(source, "external-clone", false);
        CHECK(&fixture.Registry->Get<ProbeA>(externalClone).Target.Get() == &fixture.Registry->Get<Transform>(external));
        CHECK(&fixture.Registry->Get<ProbeA>(source).Target.Get() == &fixture.Registry->Get<Transform>(external));
    });
}

TEST_CASE("ABI-03 Read request after engine Stop rejects without unbounded task wait", "[native][engine-integration][api][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture fixture;
        fixture.Start();
        fixture.Stop();
        char output[128] = "guard";
        CHECK(GetLoadedAssetState("absent", output, 128) == 0);
    }, 15000, 1024);
}
