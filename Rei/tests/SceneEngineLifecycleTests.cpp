#include "pch.h"
#include "support/NativeRenderPipelineFixture.h"
#include "Modules/Scenes/SceneManager.h"
#include "Api/AssetApi.h"

using namespace rei;
using namespace rei::tests;
using namespace rei::internal::engine;

namespace
{
    using nlohmann::json;

    struct SceneReadback
    {
        ecs::Entity Parent = ecs::NULL_ENTITY;
        ecs::Entity Child = ecs::NULL_ENTITY;
        std::string ParentName;
        i32 Number = 0;
        i32 EntityCount = 0;
        i32 LoadedAssets = 0;
        bool ParentResolved = false;
        bool TargetResolved = false;
        bool Running = false;
        GLFWwindow* Window = nullptr;
    };

    json SwitchingEntities(const std::string& name, const i32 number)
    {
        return json::array({
            SceneObject(8, name + " child", json::array({
                SceneBehaviour(PROBE_TRANSFORM, SerializedField("_parent", 7)),
                SceneBehaviour(PROBE_A, SerializedField("Target", SerializedField("SceneEntityId", 7)))})),
            SceneObject(7, name + " parent", json::array({SceneBehaviour(PROBE_TRANSFORM), SceneBehaviour(PROBE_A, SerializedField("Number", number))}))
        });
    }

    void SwitchingScenes(TemporaryDirectory& files)
    {
        PrepareEngineResources(files, "scene-switch", SwitchingEntities("A", 11), {
            {"0", "build-scenes", JsonAsset({{"Scenes", {{"0", "empty-scene"}, {"1", "scene-b"}, {"2", "missing-dependency"},
                {"3", "malformed-scene"}, {"4", "unknown-behaviour"}, {"5", "failed-init"}, {"6", "duplicate-ids"}, {"7", "missing-payload"},
                {"8", "missing-behaviour-id"}, {"9", "failed-assets"}}}})},
            {"scene-b", "B", JsonAsset({{"Name", "B"}, {"Entities", SwitchingEntities("B", 91)}})},
            {"missing-dependency", "missing dependency", JsonAsset({{"Name", "invalid"}, {"Entities", json::array({
                SceneObject(7, "dependent", json::array({SceneBehaviour(PROBE_TRANSFORM),
                    SceneBehaviour(PROBE_A, SerializedField("Asset", SerializedField("Id", "missing")))}))})}})},
            {"malformed-scene", "malformed", JsonAsset({{"Name", "invalid"}, {"Entities", json::array({json::object()})}})},
            {"unknown-behaviour", "unknown", JsonAsset({{"Name", "invalid"}, {"Entities", json::array({
                SceneObject(9, "unfinished", json::array()), SceneObject(7, "unknown", json::array({SceneBehaviour(9999)}))})}})},
            {"failed-init", "failed init", JsonAsset({{"Name", "invalid"}, {"Entities", json::array({
                SceneObject(9, "unfinished", json::array()),
                SceneObject(7, "failed init", json::array({SceneBehaviour(PROBE_TRANSFORM), SceneBehaviour(PROBE_A, SerializedField("Number", 666))}))})}})},
            {"duplicate-ids", "duplicates", JsonAsset({{"Name", "invalid"}, {"Entities", json::array({
                SceneObject(7, "first", json::array()), SceneObject(7, "duplicate", json::array())})}})},
            {"missing-behaviour-id", "missing behaviour ID", JsonAsset({{"Name", "invalid"}, {"Entities", json::array({
                SceneObject(9, "unfinished", json::array()), SceneObject(7, "missing ID", json::array({json::object()}))})}})},
            {"failed-assets", "failed assets", JsonAsset({{"Name", "invalid"}, {"Entities", json::array({
                SceneObject(9, "unfinished", json::array()),
                SceneObject(7, "failed assets", json::array({SceneBehaviour(PROBE_TRANSFORM), SceneBehaviour(PROBE_A, SerializedField("Number", 777))}))})}})}
        });
    }

    SceneReadback ReadScene(NativeEngineFixture& fixture)
    {
        SceneReadback result;
        fixture.OnEngineThread([&]
        {
            const auto registry = GetInternalWorld()->GetRegistry();
            result.Parent = GetEntityManager().GetBySceneId(7);
            result.Child = GetEntityManager().GetBySceneId(8);
            result.EntityCount = static_cast<i32>(GetInternalWorld()->GetFiltersRegistry()->Get<EntityInfo>()->Entities().size());
            result.LoadedAssets = GetAssetManager().GetLoadedAssetCount();
            result.Running = fixture.Engine->IsRunning();
            result.Window = glfwGetCurrentContext();
            if (result.Parent == ecs::NULL_ENTITY || result.Child == ecs::NULL_ENTITY) return;
            result.ParentName = registry->Get<EntityInfo>(result.Parent).Name;
            result.Number = registry->Get<ProbeA>(result.Parent).Number;
            result.ParentResolved = registry->Get<Transform>(result.Child).GetParent() == result.Parent;
            const auto& target = registry->Get<ProbeA>(result.Child).Target;
            result.TargetResolved = !target.IsNull() && &target.Get() == &registry->Get<Transform>(result.Parent);
        });
        return result;
    }

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
            auto& manager = GetSceneManager();
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
            manager.UnloadCurrentScene();
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

TEST_CASE("SCENE-04 Actual engine switches and reloads scenes with fresh handles and references", "[native][engine-integration][scene][regression][isolated]")
{
    Isolated([]
    {
        const auto mode = GENERATE(EditorMode, PlayMode);
        NativeEngineFixture fixture(mode, "scene-switch", SwitchingScenes);
        fixture.Start();
        fixture.WaitForNextFrames();
        const auto initial = ReadScene(fixture);
        REQUIRE(initial.EntityCount == 2);
        REQUIRE(initial.Number == 11);
        REQUIRE(initial.ParentResolved);
        REQUIRE(initial.TargetResolved);
        auto previous = initial;
        auto* engine = fixture.Engine.get();
        scenes::SceneManager* manager = nullptr;
        ecs::Entity internalEntity = ecs::NULL_ENTITY;
        fixture.OnEngineThread([&]
        {
            manager = &GetSceneManager();
            internalEntity = GetInternalWorld()->GetRegistry()->NewEntity();
        });
        for (const i32 sceneId : {1, 0, 0})
        {
            bool loaded = false, previousAlive = true, internalAlive = false, sameManager = false;
            fixture.OnEngineThread([&]
            {
                loaded = GetSceneManager().LoadScene(sceneId);
                const auto registry = GetInternalWorld()->GetRegistry();
                previousAlive = registry->IsAlive(previous.Parent) || registry->IsAlive(previous.Child);
                internalAlive = registry->IsAlive(internalEntity);
                sameManager = manager == &GetSceneManager();
            });
            REQUIRE(loaded);
            CHECK_FALSE(previousAlive);
            CHECK(internalAlive);
            CHECK(sameManager);
            fixture.WaitForNextFrames();
            const auto current = ReadScene(fixture);
            CHECK(current.Running);
            CHECK(current.Window == initial.Window);
            CHECK(fixture.Engine.get() == engine);
            CHECK(current.EntityCount == 2);
            CHECK(current.Number == (sceneId == 1 ? 91 : 11));
            CHECK(current.ParentName == (sceneId == 1 ? "B parent" : "A parent"));
            CHECK(current.ParentResolved);
            CHECK(current.TargetResolved);
            CHECK(current.LoadedAssets == initial.LoadedAssets);
            CHECK(fixture.App->Starts == 1);
            CHECK(fixture.App->Shutdowns == 0);
            previous = current;
        }
        i32 disposed = 0;
        fixture.OnEngineThread([&] { disposed = fixture.Resources.Count(PROBE_A, "Dispose"); });
        CHECK(disposed == (mode == PlayMode ? 6 : 0));
        fixture.Stop();
    }, 30000, 1024);
}

TEST_CASE("SCENE-04 Actual engine stays empty after failed transition and accepts the next scene", "[native][engine-integration][scene][regression][isolated]")
{
    Isolated([]
    {
        const auto mode = GENERATE(EditorMode, PlayMode);
        const i32 invalidScene = GENERATE(2, 3, 4, 5, 6, 7, 8, 9, 99);
        NativeEngineFixture fixture(mode, "scene-switch", SwitchingScenes);
        fixture.Start();
        fixture.WaitForNextFrames();
        const auto initial = ReadScene(fixture);
        fixture.OnEngineThread([&]
        {
            fixture.Resources.Events->Action = [](const i32 id, const std::string& stage, const ecs::Entity entity)
            {
                if (id != PROBE_A) return;
                const auto number = GetInternalWorld()->GetRegistry()->Get<ProbeA>(entity).Number;
                if (stage == "Init" && number == 666) throw std::runtime_error("Scene Init probe failure");
                if (stage == "LoadAssets" && number == 777) throw std::runtime_error("Scene LoadAssets probe failure");
            };
        });
        bool loaded = true, previousAlive = true;
        fixture.OnEngineThread([&]
        {
            loaded = GetSceneManager().LoadScene(invalidScene);
            const auto registry = GetInternalWorld()->GetRegistry();
            previousAlive = registry->IsAlive(initial.Parent) || registry->IsAlive(initial.Child);
            fixture.Resources.Events->Action = {};
        });
        CHECK_FALSE(loaded);
        CHECK_FALSE(previousAlive);
        fixture.WaitForNextFrames();
        const auto empty = ReadScene(fixture);
        CHECK(empty.EntityCount == 0);
        CHECK(empty.Running);
        CHECK(empty.Window == initial.Window);
        CHECK(empty.LoadedAssets == initial.LoadedAssets - 1);
        fixture.OnEngineThread([&] { loaded = GetSceneManager().LoadScene(1); });
        REQUIRE(loaded);
        fixture.WaitForNextFrames();
        const auto current = ReadScene(fixture);
        CHECK(current.EntityCount == 2);
        CHECK(current.ParentName == "B parent");
        CHECK(current.Number == 91);
        CHECK(current.ParentResolved);
        CHECK(current.TargetResolved);
        CHECK(current.LoadedAssets == initial.LoadedAssets);
        CHECK(fixture.App->Starts == 1);
        CHECK(fixture.App->Shutdowns == 0);
        fixture.Stop();
    }, 60000, 1024);
}

TEST_CASE("SCENE-04 Actual engine finishes unloading when a behaviour Dispose throws", "[native][engine-integration][scene][regression][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture fixture(PlayMode, "scene-switch", SwitchingScenes);
        fixture.Start();
        fixture.WaitForNextFrames();
        const auto initial = ReadScene(fixture);
        auto disposed = std::make_shared<std::vector<ecs::Entity>>();
        fixture.OnEngineThread([&]
        {
            fixture.Resources.Events->Action = [disposed, child = initial.Child](const i32 id, const std::string& stage, const ecs::Entity entity)
            {
                if (id != PROBE_A || stage != "Dispose") return;
                disposed->push_back(entity);
                if (entity == child) throw std::runtime_error("Scene Dispose probe failure");
            };
        });
        bool loaded = false, previousAlive = true;
        fixture.OnEngineThread([&]
        {
            loaded = GetSceneManager().LoadScene(1);
            const auto registry = GetInternalWorld()->GetRegistry();
            previousAlive = registry->IsAlive(initial.Parent) || registry->IsAlive(initial.Child);
            fixture.Resources.Events->Action = {};
        });
        REQUIRE(loaded);
        CHECK_FALSE(previousAlive);
        CHECK(*disposed == std::vector<ecs::Entity>{initial.Child, initial.Parent});
        fixture.WaitForNextFrames();
        const auto current = ReadScene(fixture);
        CHECK(current.Number == 91);
        CHECK(current.EntityCount == 2);
        CHECK(current.Running);
        CHECK(fixture.App->Shutdowns == 0);
        const auto logs = common::logging::GetRecentLogEntriesSnapshot();
        CHECK(std::any_of(logs.begin(), logs.end(), [](const std::string& entry) { return entry.find("Scene Dispose probe failure") != std::string::npos; }));
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

TEST_CASE("SCENE-03 Shutdown destroys orphan runtime asset consumers before unloading assets", "[native][engine-integration][scene][regression][isolated]")
{
    const auto mode = GENERATE(EditorMode, PlayMode);
    Isolated([mode]
    {
        NativeEngineFixture fixture(mode);
        fixture.Start();
        std::shared_ptr<ecs::EcsRegistry> registry;
        std::vector<ecs::Entity> consumers;
        fixture.OnEngineThread([&]
        {
            registry = GetInternalWorld()->GetRegistry();
            for (i32 i = 0; i < 2; ++i)
            {
                const auto entity = CreateMesh(2, render::Color::White());
                // Internal runtime entities need not belong to EntityManager's scene roots.
                registry->Del<EntityInfo>(entity);
                consumers.push_back(entity);
            }
            GetInternalWorld()->Refresh();
        });
        fixture.Stop();
        for (const auto entity : consumers) CHECK_FALSE(registry->IsAlive(entity));
        CHECK(GetAssetManager().GetLoadedAssetCount() == 0);
        fixture.Engine.reset();
        const auto logs = common::logging::GetRecentLogEntriesSnapshot();
        CHECK(std::none_of(logs.begin(), logs.end(), [](const std::string& entry)
        {
            return entry.find("Missing asset with id: runtime_asset_") != std::string::npos ||
                entry.find("Failed to load asset name=runtime_asset_") != std::string::npos;
        }));
    }, 20000, 1024);
}
