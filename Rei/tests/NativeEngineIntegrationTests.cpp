#include "pch.h"
#include "support/NativeEngineFixture.h"
#include "Api/AssetApi.h"
#include "Api/EntityApi.h"
#include "Api/EditorApi.h"
#include "Modules/Behaviour/Components/BehaviourCollection.h"
#include "Modules/Components/ActiveTag.h"

using namespace rei;
using namespace rei::tests;
using namespace rei::internal::engine;

TEST_CASE("LIFE-01 Actual engine starts updates stops and releases runtime assets", "[native][engine-integration][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture fixture;
        fixture.Start();
        REQUIRE(fixture.Engine->IsRunning());
        REQUIRE(fixture.App->Starts == 1);
        i32 frames = 0;
        i32 loaded = 0;
        fixture.OnEngineThread([&] { frames = fixture.App->Updates; loaded = GetAssetManager().GetLoadedAssetCount(); });
        CHECK(frames > 0);
        CHECK(loaded > 0);
        fixture.Stop(37);
        CHECK_FALSE(fixture.Engine->IsRunning());
        CHECK(fixture.Engine->GetExitCode() == 37);
        CHECK(fixture.App->Shutdowns == 1);
        CHECK(GetAssetManager().GetLoadedAssetsSize() == 0);
        fixture.Engine->Shutdown(99);
        CHECK(fixture.App->Shutdowns == 1);
        CHECK(fixture.Engine->GetExitCode() == 37);
    }, 20000, 1024);
}

TEST_CASE("ABI-02 Actual API reports unloaded unsupported read failed and loaded without implicit load", "[native][engine-integration][api][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture fixture;
        AssetProbeScope probe;
        fixture.Start();
        fixture.OnEngineThread([]
        {
            GetAssetManager().CreateAssetWithId<AssetProbe>("loaded", 123);
            GetAssetManager().CreateAssetWithId<UnsupportedAsset>("unsupported");
            GetAssetManager().CreateAssetWithId<AssetProbe>("read-failure", 5);
        });
        auto state = [](const char* id)
        {
            std::vector<char> probeBuffer(1, 'x');
            const auto required = GetLoadedAssetState(id, probeBuffer.data(), 1);
            REQUIRE(required > 1);
            REQUIRE(probeBuffer[0] == '\0');
            std::vector<char> output(required + 1, 'x');
            REQUIRE(GetLoadedAssetState(id, output.data(), required) == required);
            REQUIRE(output[required] == 'x');
            return nlohmann::json::parse(output.data());
        };
        CHECK(state("not-in-map").at("status") == "unloaded");
        CHECK(state("unsupported").at("status") == "unsupported");
        CHECK(state("loaded").at("status") == "loaded");
        CHECK(state("loaded").at("values").at("Value") == 123);
        fixture.OnEngineThread([&] { probe.State->ThrowGet = true; });
        CHECK(state("read-failure").at("status") == "read_failed");
        fixture.OnEngineThread([&] { probe.State->ThrowGet = false; });
        CHECK(probe.State->Attempts == 2);
        fixture.Stop();
    }, 20000, 1024);
}

TEST_CASE("ABI-03 Controller changes entity name and native reads agree independently", "[native][engine-integration][api][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture fixture;
        fixture.Start();
        ecs::Entity entity = ecs::NULL_ENTITY;
        fixture.OnEngineThread([&] { entity = GetEntityManager().CreateNewEntity("first"); });
        i32 sceneId = -1;
        fixture.OnEngineThread([&] { sceneId = GetInternalWorld()->GetRegistry()->Get<EntityInfo>(entity).Id; });
        RenameEntity(sceneId, "\xD0\xA0\xD0\xB5\xD0\xB9");
        std::string nativeName;
        fixture.OnEngineThread([&] { nativeName = GetInternalWorld()->GetRegistry()->Get<EntityInfo>(entity).Name; });
        CHECK(nativeName == "\xD0\xA0\xD0\xB5\xD0\xB9");
        char json[4096]{};
        GetEntityData(sceneId, json, 4096);
        CHECK(nlohmann::json::parse(json).at("Name") == nativeName);
        fixture.Stop();
    }, 20000, 1024);
}

TEST_CASE("TASK-04 Synchronous API called from engine task must terminate without deadlock", "[native][engine-integration][api][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture fixture;
        fixture.Start();
        fixture.OnEngineThread([] { char output[512]{}; GetLoadedAssetState("absent", output, 512); });
        fixture.Stop();
    }, 15000, 1024);
}

TEST_CASE("BEH-04 Actual play engine destroys subtree with one Dispose per instance", "[native][engine-integration][behaviour][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture fixture;
        fixture.Start();
        ecs::Entity parent = ecs::NULL_ENTITY, child = ecs::NULL_ENTITY, outsider = ecs::NULL_ENTITY;
        std::vector<ecs::Entity> disposed;
        fixture.OnEngineThread([&]
        {
            parent = GetEntityManager().CreateNewEntity("parent");
            child = GetEntityManager().CreateNewEntity("child");
            outsider = GetEntityManager().CreateNewEntity("outsider");
            auto registry = GetInternalWorld()->GetRegistry();
            registry->Get<Transform>(child).SetParent(parent);
            GetEntityManager().AddBehaviour(parent, PROBE_A, nlohmann::json());
            GetEntityManager().AddBehaviour(child, PROBE_A, nlohmann::json());
            GetEntityManager().AddBehaviour(outsider, PROBE_A, nlohmann::json());
            fixture.Resources.Events->Action = [&](i32, const std::string& stage, const ecs::Entity entity)
            {
                if (stage == "Dispose") disposed.push_back(entity);
            };
            GetEntityManager().Destroy(parent);
            GetEntityManager().Destroy(parent);
            GetInternalWorld()->Refresh();
        });
        bool parentAlive = true, childAlive = true, outsiderAlive = false;
        fixture.OnEngineThread([&]
        {
            auto registry = GetInternalWorld()->GetRegistry();
            parentAlive = registry->IsAlive(parent);
            childAlive = registry->IsAlive(child);
            outsiderAlive = registry->IsAlive(outsider);
            fixture.Resources.Events->Action = {};
        });
        CHECK_FALSE(parentAlive);
        CHECK_FALSE(childAlive);
        CHECK(outsiderAlive);
        REQUIRE(disposed.size() == 2);
        CHECK(disposed[0] == child);
        CHECK(disposed[1] == parent);
        fixture.Stop();
    }, 20000, 1024);
}

TEST_CASE("BEH-05 Editor engine suppresses Start Update and play Dispose", "[native][engine-integration][behaviour][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture fixture(EditorMode);
        fixture.Start();
        i32 starts = -1, updates = -1, disposes = -1;
        ecs::Entity entity = ecs::NULL_ENTITY;
        fixture.OnEngineThread([&]
        {
            entity = GetEntityManager().CreateNewEntity("editor");
            GetEntityManager().AddBehaviour(entity, PROBE_A, nlohmann::json());
        });
        fixture.WaitForNextFrames(2);
        fixture.OnEngineThread([&]
        {
            GetEntityManager().Destroy(entity);
            GetInternalWorld()->Refresh();
            starts = fixture.Resources.Count(PROBE_A, "Start");
            updates = fixture.Resources.Count(PROBE_A, "Update");
            disposes = fixture.Resources.Count(PROBE_A, "Dispose");
        });
        CHECK(starts == 0);
        CHECK(updates == 0);
        CHECK(disposes == 0);
        fixture.Stop();
    }, 20000, 1024);
}

TEST_CASE("LIFE-02 Second engine session composes shader includes from new resource pack", "[native][engine-integration][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        {
            NativeEngineFixture fixture(PlayMode, "SESSION_A");
            fixture.Start();
            CHECK(render::ShaderGenerator::GetInstance().ComposeVertexSource("").find("SESSION_A") != std::string::npos);
            fixture.Stop();
        }
        NativeEngineFixture fixture(PlayMode, "SESSION_B");
        fixture.Start();
        const auto source = render::ShaderGenerator::GetInstance().ComposeVertexSource("");
        CHECK(source.find("SESSION_B") != std::string::npos);
        CHECK(source.find("SESSION_A") == std::string::npos);
        fixture.Stop();
    }, 25000, 1536);
}

TEST_CASE("LIFE-01 Startup exception exits before readiness and performs shutdown", "[native][engine-integration][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture fixture;
        fixture.App->StartAction = [] { throw std::runtime_error("native startup probe"); };
        REQUIRE_THROWS_WITH(fixture.Start(), Catch::Matchers::ContainsSubstring("Native engine exited before readiness"));
        fixture.Stop();
        CHECK_FALSE(fixture.Engine->IsRunning());
        CHECK(fixture.Engine->GetExitCode() == ENGINE_INITIALIZATION_ERROR_EXIT_CODE);
        CHECK(fixture.App->Starts == 1);
        CHECK(fixture.App->Shutdowns == 1);
        CHECK(GetAssetManager().GetLoadedAssetsSize() == 0);
    }, 20000, 1024);
}

TEST_CASE("LIFE-01 Update exception stops engine and releases assets", "[native][engine-integration][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture fixture;
        fixture.App->UpdateAction = [] { throw std::runtime_error("native update probe"); };
        fixture.Start();
        fixture.Stop();
        CHECK_FALSE(fixture.Engine->IsRunning());
        CHECK(fixture.Engine->GetExitCode() == ENGINE_UPDATE_ERROR_EXIT_CODE);
        CHECK(fixture.App->Updates == 1);
        CHECK(fixture.App->Shutdowns == 1);
        CHECK(GetAssetManager().GetLoadedAssetsSize() == 0);
    }, 20000, 1024);
}

TEST_CASE("LIFE-01 Shutdown exception must not abandon runtime asset cleanup", "[native][engine-integration][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture fixture;
        fixture.App->ShutdownAction = [] { throw std::runtime_error("native shutdown probe"); };
        fixture.Start();
        CHECK_THROWS_WITH(fixture.Stop(), "native shutdown probe");
        CHECK_FALSE(fixture.Engine->IsRunning());
        CHECK(fixture.App->Shutdowns == 1);
        CHECK(GetAssetManager().GetLoadedAssetCount() == 0);
        CHECK(GetAssetManager().GetLoadedAssetsSize() == 0);
    }, 20000, 1024);
}

TEST_CASE("BEH-04 Reentrant Dispose of own entity remains bounded and happens once", "[native][engine-integration][behaviour][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture fixture;
        fixture.Start();
        i32 disposed = 0;
        fixture.OnEngineThread([&]
        {
            const auto entity = GetEntityManager().CreateNewEntity("reentrant");
            GetEntityManager().AddBehaviour(entity, PROBE_A, nlohmann::json());
            fixture.Resources.Events->Action = [&](i32, const std::string& stage, const ecs::Entity current)
            {
                if (stage != "Dispose") return;
                ++disposed;
                GetEntityManager().Destroy(current);
            };
            GetEntityManager().Destroy(entity);
            GetInternalWorld()->Refresh();
            fixture.Resources.Events->Action = {};
        });
        CHECK(disposed == 1);
        fixture.Stop();
    }, 20000, 1024);
}

namespace
{
    thread_local std::vector<nlohmann::json>* ScenePackets = nullptr;
    void ReceiveScenePacket(const char* json, const i32 byteCount)
    {
        if (ScenePackets) ScenePackets->push_back(nlohmann::json::parse(json, json + byteCount));
    }
}

TEST_CASE("Scene relay sends initial snapshot and only changed hierarchy metadata", "[native][scene-sync][engine-integration][isolated]")
{
    Isolated([]
    {
        std::vector<nlohmann::json> packets;
        NativeEngineFixture fixture(EditorMode, "scene-sync");
        fixture.Start();
        fixture.OnEngineThread([&]
        {
            ECS_WORLD(GetInternalWorld());
            ScenePackets = &packets;
            SetSceneSyncCallback(ReceiveScenePacket);
            GetSceneSyncRelay().Capture(GetInternalWorld());
            REQUIRE(packets.size() == 1);
            CHECK(packets.back()["IsSnapshot"] == true);
            CHECK(packets.back()["Entities"].empty());
            GetSceneSyncRelay().Capture(GetInternalWorld());
            CHECK(packets.size() == 1);

            const auto first = GetEntityManager().CreateNewEntity("first");
            const auto second = GetEntityManager().CreateNewEntity("second");
            const auto firstId = GET(first, EntityInfo).Id;
            const auto secondId = GET(second, EntityInfo).Id;
            GET(first, Transform).SetChildOrder(0);
            GET(second, Transform).SetChildOrder(1);
            editor::selection_utility::Select(GetInternalWorld(), second);
            GetInternalWorld()->Refresh();
            GetSceneSyncRelay().Capture(GetInternalWorld());
            REQUIRE(packets.size() == 2);
            CHECK(packets.back()["IsSnapshot"] == false);
            CHECK(packets.back()["Entities"] == nlohmann::json::array({
                {{"Id", firstId}, {"Name", "first"}, {"ParentId", 0}, {"Order", 0}},
                {{"Id", secondId}, {"Name", "second"}, {"ParentId", 0}, {"Order", 1}}
            }));
            CHECK(packets.back()["SelectedEntityIds"] == nlohmann::json::array({secondId}));

            GET(first, EntityInfo).Name = "renamed";
            GET(second, Transform).SetParent(first);
            GET(second, Transform).SetChildOrder(4);
            GetSceneSyncRelay().Capture(GetInternalWorld());
            REQUIRE(packets.size() == 3);
            CHECK(packets.back()["Entities"][0]["Name"] == "renamed");
            CHECK(packets.back()["Entities"][1]["ParentId"] == firstId);
            CHECK(packets.back()["Entities"][1]["Order"] == 4);

            // Property and behaviour changes belong to Monitor, not the scene stream.
            GET(first, Transform).Translate(math::Vector3{1, 2, 3});
            GetEntityManager().AddBehaviour(first, PROBE_A, nlohmann::json());
            GetInternalWorld()->Refresh();
            GetSceneSyncRelay().Capture(GetInternalWorld());
            CHECK(packets.size() == 3);

            GET(second, Transform).SetParent(ecs::NULL_ENTITY);
            GET(second, Transform).SetChildOrder(0);
            GetEntityManager().Destroy(first);
            GetInternalWorld()->Refresh();
            GetSceneSyncRelay().Capture(GetInternalWorld());
            REQUIRE(packets.size() == 4);
            CHECK(packets.back()["RemovedEntityIds"] == nlohmann::json::array({firstId}));
            CHECK(packets.back()["Entities"][0]["Id"] == secondId);
            CHECK(packets.back()["Entities"][0]["ParentId"] == 0);

            editor::selection_utility::Reset(GetInternalWorld());
            GetInternalWorld()->Refresh();
            GetSceneSyncRelay().Capture(GetInternalWorld());
            REQUIRE(packets.size() == 5);
            CHECK(packets.back()["Entities"].empty());
            CHECK(packets.back()["SelectedEntityIds"].empty());

            GetSceneSyncRelay().RequestSnapshot();
            GetSceneSyncRelay().Capture(GetInternalWorld());
            REQUIRE(packets.size() == 6);
            CHECK(packets.back()["IsSnapshot"] == true);
            CHECK(packets.back()["Entities"].size() == 1);
            CHECK(packets.back()["RemovedEntityIds"].empty());
            GetSceneSyncRelay().Capture(GetInternalWorld());
            CHECK(packets.size() == 6);
            SetSceneSyncCallback(nullptr);
            GetSceneSyncRelay().Capture(GetInternalWorld());
            CHECK(packets.size() == 6);
            ScenePackets = nullptr;
        });
        fixture.Stop();
    });
}

TEST_CASE("Scene frame stream serves embedded Play and stays silent in standalone Play", "[native][scene-sync][engine-integration][isolated]")
{
    Isolated([]
    {
        for (const bool embeddedEditor : {false, true})
        {
            std::vector<nlohmann::json> packets;
            NativeEngineFixture fixture(PlayMode, "scene-play", {}, embeddedEditor);
            fixture.Start();
            i32 sceneId = 0;
            fixture.OnEngineThread([&]
            {
                ECS_WORLD(GetInternalWorld());
                ScenePackets = &packets;
                SetSceneSyncCallback(ReceiveScenePacket);
                const auto entity = GetEntityManager().CreateNewEntity("selected");
                sceneId = GET(entity, EntityInfo).Id;
                editor::selection_utility::Select(GetInternalWorld(), entity);
            });
            fixture.WaitForNextFrames();
            fixture.OnEngineThread([&]
            {
                if (embeddedEditor)
                {
                    REQUIRE(packets.size() == 1);
                    CHECK(packets[0]["IsSnapshot"] == true);
                    CHECK(packets[0]["Entities"][0]["Id"] == sceneId);
                    CHECK(packets[0]["SelectedEntityIds"] == nlohmann::json::array({sceneId}));
                }
                else CHECK(packets.empty());
                SetSceneSyncCallback(nullptr);
                ScenePackets = nullptr;
            });
            fixture.Stop();
        }
    });
}

TEST_CASE("Scene stream does not publish engine shutdown as scene deletion", "[native][scene-sync][engine-integration][isolated]")
{
    Isolated([]
    {
        std::vector<nlohmann::json> packets;
        NativeEngineFixture fixture(EditorMode, "scene-stop");
        fixture.Start();
        fixture.OnEngineThread([&]
        {
            ScenePackets = &packets;
            SetSceneSyncCallback(ReceiveScenePacket);
            GetEntityManager().CreateNewEntity("keep in Editor");
        });
        fixture.WaitForNextFrames();
        fixture.Stop();
        REQUIRE(packets.size() == 1);
        CHECK(packets[0]["Entities"].size() == 1);
    });
}

TEST_CASE("Selection API applies multi-select and clear", "[native][scene-sync][engine-integration][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture fixture(EditorMode);
        fixture.Start();
        std::vector<i32> ids;
        fixture.OnEngineThread([&]
        {
            ECS_WORLD(GetInternalWorld());
            for (const auto name : {"first", "second"})
            {
                const auto entity = GetEntityManager().CreateNewEntity(name);
                ids.push_back(GET(entity, EntityInfo).Id);
            }
        });
        const auto request = nlohmann::json{{"EntityIds", ids}}.dump();
        SetEntitySelection(request.c_str());
        fixture.OnEngineThread([&]
        {
            ECS_WORLD(GetInternalWorld());
            GetInternalWorld()->Refresh();
            std::vector<i32> nativeIds;
            const auto& selected = FILTER(EntityInfo, editor::SelectedTag);
            FOR(entity, selected) nativeIds.push_back(GET(entity, EntityInfo).Id);
            std::sort(nativeIds.begin(), nativeIds.end());
            CHECK(nativeIds == ids);
        });
        SetEntitySelection(R"({"EntityIds":[]})");
        fixture.OnEngineThread([&]
        {
            ECS_WORLD(GetInternalWorld());
            GetInternalWorld()->Refresh();
            CHECK(FILTER(EntityInfo, editor::SelectedTag)->GetEntitiesCount() == 0);
        });
        fixture.Stop();
    });
}
