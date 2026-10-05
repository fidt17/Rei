#include "pch.h"
#include "support/BehaviourTestFixture.h"
#include "Modules/Components/ActiveTag.h"

using namespace rei;
using namespace rei::ecs;
using namespace rei::tests;

TEST_CASE("BEH-01 Native initialization and frame stages produce ordered lifecycle trace", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A);
        fixture.Frame();
        fixture.Frame();
        // Explicit virtual Dispose; Engine playmode Delete/Destroy dispatch is separate coverage.
        fixture.Manager->GetBehaviour(entity, PROBE_A).Dispose();
        fixture.Manager->GetBehaviourRegistry().DeleteBehaviour(entity, PROBE_A);
        fixture.Frame();
        CHECK(fixture.Events->Calls == std::vector<std::string>{"7101:LoadAssets", "7101:Init", "7101:Start", "7101:Update", "7101:Update", "7101:Dispose"});
        CHECK_FALSE(fixture.Registry->Has<ProbeA>(entity));
        CHECK(fixture.Registry->Get<BehaviourCollection>(entity).Behaviours.empty());
        CHECK_FALSE(fixture.Registry->Has<StartBehavioursEvent>(entity));
    });
}

TEST_CASE("BEH-01 Deferred add initializes only on explicit InitBehaviour", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        CHECK(fixture.Events->Calls.empty());
        CHECK_FALSE(fixture.Registry->Has<StartBehavioursEvent>(entity));
        fixture.Manager->InitBehaviour(entity, fixture.Registry->Get<ProbeA>(entity));
        fixture.Frame();
        CHECK(fixture.Events->Calls == std::vector<std::string>{"7101:LoadAssets", "7101:Init", "7101:Start", "7101:Update"});
    });
}

TEST_CASE("BEH-01 Duplicate add rejects without extra initialization or queue entry", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A);
        fixture.Registry->Get<ProbeA>(entity).Number = 123;
        CHECK_THROWS(fixture.Add(entity, PROBE_A));
        CHECK(fixture.Registry->Get<ProbeA>(entity).Number == 123);
        CHECK(fixture.Registry->Get<BehaviourCollection>(entity).Behaviours == std::vector<i32>{PROBE_A});
        CHECK(fixture.Registry->Get<StartBehavioursEvent>(entity).Behaviours == std::vector<i32>{PROBE_A});
        CHECK(fixture.Count(PROBE_A, "Init") == 1);
    });
}

TEST_CASE("BEH-01 Unknown behaviour IDs reject without creating components", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        CHECK_THROWS(fixture.Add(entity, 9999));
        CHECK_THROWS(fixture.Manager->GetBehaviour(entity, 9999));
        CHECK_FALSE(fixture.Registry->Has<BehaviourCollection>(entity));
        CHECK_FALSE(fixture.Registry->Has<StartBehavioursEvent>(entity));
        CHECK(fixture.Events->Calls.empty());
    });
}

TEST_CASE("BEH-02 Behaviour added during Start retains pending Start before Update", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A);
        fixture.Events->Action = [&](const i32 id, const std::string& stage, const Entity current)
        {
            if (id == PROBE_A && stage == "Start") fixture.Add(current, PROBE_B);
        };
        fixture.Frame();
        CHECK(fixture.Count(PROBE_B, "Update") == 0);
        fixture.Events->Action = {};
        fixture.Frame();
        CHECK(fixture.Count(PROBE_B, "Start") == 1);
        CHECK(fixture.Count(PROBE_B, "Update") == 1);
    });
}

TEST_CASE("BEH-02 Start deletion does not resurrect later queued component", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A);
        fixture.Add(entity, PROBE_B);
        fixture.Events->Action = [&](const i32 id, const std::string& stage, const Entity current)
        {
            if (id == PROBE_A && stage == "Start") fixture.Manager->GetBehaviourRegistry().DeleteBehaviour(current, PROBE_B);
        };
        fixture.Frame();
        CHECK_FALSE(fixture.Registry->Has<ProbeB>(entity));
        CHECK(fixture.Count(PROBE_B, "Start") == 0);
        CHECK(fixture.Count(0, "Start") == 0);
        CHECK(fixture.Registry->Get<BehaviourCollection>(entity).Behaviours == std::vector<i32>{PROBE_A});
    });
}

TEST_CASE("BEH-02 Update deletion does not resurrect later snapshot component", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A);
        fixture.Add(entity, PROBE_B);
        fixture.Events->Action = [&](const i32 id, const std::string& stage, const Entity current)
        {
            if (id == PROBE_A && stage == "Update") fixture.Manager->GetBehaviourRegistry().DeleteBehaviour(current, PROBE_B);
        };
        fixture.Frame();
        CHECK_FALSE(fixture.Registry->Has<ProbeB>(entity));
        CHECK(fixture.Count(PROBE_B, "Update") == 0);
        CHECK(fixture.Count(0, "Update") == 0);
    });
}

TEST_CASE("BEH-02 Update addition starts on next frame without same-snapshot update", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A);
        bool added = false;
        fixture.Events->Action = [&](const i32 id, const std::string& stage, const Entity current)
        {
            if (id != PROBE_A || stage != "Update" || added) return;
            added = true;
            fixture.Add(current, PROBE_B);
        };
        fixture.Frame();
        CHECK(fixture.Count(PROBE_B, "Init") == 1);
        CHECK(fixture.Count(PROBE_B, "Start") == 0);
        CHECK(fixture.Count(PROBE_B, "Update") == 0);
        fixture.Frame();
        CHECK(fixture.Count(PROBE_B, "Start") == 1);
        CHECK(fixture.Count(PROBE_B, "Update") == 1);
    });
}

TEST_CASE("BEH-03 Disabled behaviour starts once and updates after enable", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A).Disable();
        fixture.Frame();
        CHECK(fixture.Count(PROBE_A, "Start") == 1);
        CHECK(fixture.Count(PROBE_A, "Update") == 0);
        fixture.Registry->Get<ProbeA>(entity).Enable();
        fixture.Frame();
        CHECK(fixture.Count(PROBE_A, "Start") == 1);
        CHECK(fixture.Count(PROBE_A, "Update") == 1);
    });
}

TEST_CASE("BEH-03 Inactive entity starts once and resumes Update after activation", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A);
        fixture.Registry->Del<ActiveTag>(entity);
        fixture.Frame();
        CHECK(fixture.Count(PROBE_A, "Start") == 1);
        CHECK(fixture.Count(PROBE_A, "Update") == 0);
        fixture.Registry->Get<ActiveTag>(entity);
        fixture.Frame();
        CHECK(fixture.Count(PROBE_A, "Start") == 1);
        CHECK(fixture.Count(PROBE_A, "Update") == 1);
    });
}

TEST_CASE("BEH-03 Earlier Update can disable later live component", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A);
        fixture.Add(entity, PROBE_B);
        fixture.Events->Action = [&](const i32 id, const std::string& stage, const Entity current)
        {
            if (id == PROBE_A && stage == "Update") fixture.Registry->Get<ProbeB>(current).Disable();
        };
        fixture.Frame();
        CHECK(fixture.Count(PROBE_B, "Start") == 1);
        CHECK(fixture.Count(PROBE_B, "Update") == 0);
    });
}

TEST_CASE("BEH-04 Shared required behaviour initializes once before both consumers", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_C);
        fixture.Add(entity, PROBE_D);
        CHECK(fixture.Events->Calls == std::vector<std::string>{"7102:LoadAssets", "7102:Init", "7103:LoadAssets", "7103:Init", "7104:LoadAssets", "7104:Init"});
        CHECK(fixture.Registry->Get<BehaviourCollection>(entity).Behaviours == std::vector<i32>{PROBE_B, PROBE_C, PROBE_D});
        fixture.Frame();
        CHECK(fixture.Count(PROBE_B, "Start") == 1);
        CHECK(fixture.Count(PROBE_B, "Update") == 1);
    });
}

TEST_CASE("BEH-04 Missing required registration fails before consumer creation", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Manager->GetBehaviourRegistry().RegisterComponent<ProbeC>(PROBE_C, [](Entity) { return nlohmann::json(); }, [](Entity, const nlohmann::json&) {}, nullptr, {9999});
        CHECK_THROWS(fixture.Add(entity, PROBE_C));
        CHECK_FALSE(fixture.Registry->Has<ProbeC>(entity));
        CHECK_FALSE(fixture.Registry->Has<BehaviourCollection>(entity));
        CHECK(fixture.Events->Calls.empty());
    });
}

TEST_CASE("BEH-04 Cyclic required behaviours reject with bounded explicit failure", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        auto& registry = fixture.Manager->GetBehaviourRegistry();
        registry.RegisterComponent<ProbeC>(PROBE_C, [](Entity) { return nlohmann::json(); }, [](Entity, const nlohmann::json&) {}, nullptr, {PROBE_D});
        registry.RegisterComponent<ProbeD>(PROBE_D, [](Entity) { return nlohmann::json(); }, [](Entity, const nlohmann::json&) {}, nullptr, {PROBE_C});
        CHECK_THROWS(fixture.Add(entity, PROBE_C));
        CHECK_FALSE(fixture.Registry->Has<ProbeC>(entity));
        CHECK_FALSE(fixture.Registry->Has<ProbeD>(entity));
    });
}

TEST_CASE("BEH-05 LoadAssets and Init exceptions propagate without pending Start", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        for (const std::string stage : {"LoadAssets", "Init"})
        {
            BehaviourFixture fixture;
            const auto entity = fixture.Entity();
            fixture.Events->Action = [stage](i32, const std::string& current, Entity)
            {
                if (current == stage) throw std::runtime_error("probe initialization failure");
            };
            CHECK_THROWS_AS(fixture.Add(entity, PROBE_A), std::runtime_error);
            CHECK_FALSE(fixture.Registry->Has<StartBehavioursEvent>(entity));
            CHECK(fixture.Count(PROBE_A, "LoadAssets") == 1);
            CHECK(fixture.Count(PROBE_A, "Init") == (stage == "Init" ? 1 : 0));
            // Characterize partial state; no rollback guarantee asserted here.
            CHECK(fixture.Registry->Has<ProbeA>(entity));
        }
    });
}

TEST_CASE("BEH-05 Failed initialization is ineligible for subsequent Update", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Events->Action = [](i32, const std::string& stage, Entity)
        {
            if (stage == "Init") throw std::runtime_error("probe initialization failure");
        };
        REQUIRE_THROWS(fixture.Add(entity, PROBE_A));
        fixture.Events->Action = {};
        fixture.Frame();
        CHECK(fixture.Count(PROBE_A, "Start") == 0);
        CHECK(fixture.Count(PROBE_A, "Update") == 0);
    });
}

TEST_CASE("BEH-05 Start exception leaves pending queue and prevents frame Update", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A);
        fixture.Add(entity, PROBE_B);
        fixture.Events->Action = [](const i32 id, const std::string& stage, Entity)
        {
            if (id == PROBE_A && stage == "Start") throw std::runtime_error("probe start failure");
        };
        REQUIRE_THROWS(fixture.Frame());
        CHECK(fixture.Count(PROBE_B, "Start") == 0);
        CHECK(fixture.Count(PROBE_A, "Update") == 0);
        CHECK(fixture.Registry->Get<StartBehavioursEvent>(entity).Behaviours == std::vector<i32>{PROBE_A, PROBE_B});
        fixture.Events->Action = {};
        fixture.Frame();
        CHECK(fixture.Count(PROBE_A, "Start") == 2);
        CHECK(fixture.Count(PROBE_B, "Start") == 1);
    });
}

TEST_CASE("BEH-05 Update exception skips later callback and next frame can run", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A);
        fixture.Add(entity, PROBE_B);
        fixture.Events->Action = [](const i32 id, const std::string& stage, Entity)
        {
            if (id == PROBE_A && stage == "Update") throw std::runtime_error("probe update failure");
        };
        REQUIRE_THROWS(fixture.Frame());
        CHECK(fixture.Count(PROBE_B, "Update") == 0);
        fixture.Events->Action = {};
        fixture.Frame();
        CHECK(fixture.Count(PROBE_A, "Update") == 2);
        CHECK(fixture.Count(PROBE_B, "Update") == 1);
        CHECK(fixture.Count(PROBE_A, "Start") == 1);
    });
}

TEST_CASE("BEH-01 Fixture releases native ownership and restores process resources", "[native][behaviour][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        const auto cwd = std::filesystem::current_path();
        const auto previous = GetInternalWorld();
        std::weak_ptr<World> world;
        std::weak_ptr<EcsRegistry> registry;
        std::weak_ptr<EntityManager> manager;
        std::weak_ptr<assets::AssetManager> assets;
        std::filesystem::path temporary;
        {
            BehaviourFixture fixture;
            fixture.Add(fixture.Entity(), PROBE_A);
            fixture.Frame();
            world = fixture.World;
            registry = fixture.Registry;
            manager = fixture.Manager;
            assets = fixture.Assets;
            temporary = fixture.Files.File("");
        }
        CHECK(world.expired());
        CHECK(registry.expired());
        CHECK(manager.expired());
        CHECK(assets.expired());
        CHECK(std::filesystem::current_path() == cwd);
        CHECK(GetInternalWorld() == previous);
        CHECK_FALSE(std::filesystem::exists(temporary));
        CHECK_FALSE(CurrentTrace);
        CHECK(assets::AssetRef<ProbeDataAsset>::AssignHandlerFunc == nullptr);
    });
}
