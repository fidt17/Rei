#include "pch.h"
#include "support/NativeTestSupport.h"
#include "Ecs/World.h"
#include <limits>
#include <new>
#include <utility>

using namespace rei::ecs;
using rei::tests::Isolated;

namespace
{
    template <size_t INDEX>
    struct BoundaryComponent { i32 Value = 0; };
    struct SurvivingRegistryComponent { i32 Value = 0; };
    struct SurvivingFilterComponent {};

    void CheckInvalidEntityId(const std::shared_ptr<EcsRegistry>& registry, const EntityId id, const Entity live)
    {
        const auto entityCount = registry->GetAllEntities().size();
        const auto setCount = registry->GetComponentSets().size();
        const auto value = registry->Get<BoundaryComponent<0>>(live).Value;
        for (const EntityGen generation : {EntityGen{0}, EntityGen{1}, EntityGen{255}})
        {
            CAPTURE(id, generation);
            const Entity invalid{id, generation};
            CHECK_FALSE(registry->IsAlive(invalid));
            CHECK(registry->IsDead(invalid));
            CHECK_THROWS_AS(registry->GetEntityById(id), std::runtime_error);
            CHECK_THROWS_AS(registry->GetEntityMask(invalid), std::runtime_error);
            CHECK_THROWS_AS(registry->Get<BoundaryComponent<0>>(invalid), std::runtime_error);
            CHECK_THROWS_AS(registry->Get<BoundaryComponent<69>>(invalid), std::runtime_error);
            CHECK_THROWS_AS(registry->Has<BoundaryComponent<0>>(invalid), std::runtime_error);
            CHECK_THROWS_AS(registry->Del<BoundaryComponent<0>>(invalid), std::runtime_error);
            CHECK_THROWS_AS(registry->DestroyEntity(invalid), std::runtime_error);
            CHECK(registry->GetAllEntities().size() == entityCount);
            CHECK(registry->GetComponentSets().size() == setCount);
            CHECK(registry->GetDestroyedEntities().empty());
            REQUIRE(registry->IsAlive(live));
            CHECK(registry->GetEntityById(live.Id) == live);
            CHECK(registry->Get<BoundaryComponent<0>>(live).Value == value);
        }
    }

    template <size_t... INDICES>
    void PrimeDistinctTypes(std::index_sequence<INDICES...>)
    {
        (static_cast<void>(TypeId::Get<BoundaryComponent<INDICES>>()), ...);
    }

    template <size_t... INDICES>
    void AddDistinct(const std::shared_ptr<EcsRegistry>& registry, const Entity entity, std::index_sequence<INDICES...>)
    {
        ((registry->Get<BoundaryComponent<INDICES>>(entity).Value = static_cast<i32>(INDICES + 100)), ...);
    }

    template <size_t... INDICES>
    void CheckDistinct(const std::shared_ptr<EcsRegistry>& registry, const Entity entity, std::index_sequence<INDICES...>)
    {
        const auto check = [&]<size_t INDEX>()
        {
            CAPTURE(INDEX);
            REQUIRE(registry->Has<BoundaryComponent<INDEX>>(entity));
            CHECK(registry->Get<BoundaryComponent<INDEX>>(entity).Value == static_cast<i32>(INDEX + 100));
        };
        (check.template operator()<INDICES>(), ...);
    }

    // Protect the complete former World allocation so surviving callback tests
    // cannot accidentally pass because freed heap memory still looks valid.
    class ProtectedWorld
    {
    public:
        ProtectedWorld()
        {
            _storage = VirtualAlloc(nullptr, sizeof(World), MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (!_storage) throw std::runtime_error("Could not allocate protected native World");
            _world = new (_storage) World();
        }
        ~ProtectedWorld()
        {
            if (_world) _world->~World();
            VirtualFree(_storage, 0, MEM_RELEASE);
        }
        ProtectedWorld(const ProtectedWorld&) = delete;
        ProtectedWorld& operator=(const ProtectedWorld&) = delete;
        World& Get() const { return *_world; }

        void Destroy()
        {
            _world->~World();
            _world = nullptr;
            DWORD previous = 0;
            if (!VirtualProtect(_storage, sizeof(World), PAGE_NOACCESS, &previous)) throw std::runtime_error("Could not protect destroyed native World");
        }

    private:
        void* _storage = nullptr;
        World* _world = nullptr;
    };
}

TEST_CASE("ECS-05 More than sixty-four component types retain independent values and filters", "[native][ecs][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        World world;
        const auto registry = world.GetRegistry();
        const auto entity = registry->NewEntity();
        AddDistinct(registry, entity, std::make_index_sequence<70>{});
        world.Refresh();
        CheckDistinct(registry, entity, std::make_index_sequence<70>{});
        const auto filter = world.GetFiltersRegistry()->Get<BoundaryComponent<0>, BoundaryComponent<63>, BoundaryComponent<64>, BoundaryComponent<69>>();
        CHECK(filter->Entities() == std::vector<Entity>{entity});
        registry->Del<BoundaryComponent<64>>(entity);
        world.Refresh();
        CHECK(filter->Entities().empty());
        CHECK(registry->Get<BoundaryComponent<63>>(entity).Value == 163);
        CHECK(registry->Get<BoundaryComponent<69>>(entity).Value == 169);
        registry->Get<BoundaryComponent<64>>(entity).Value = 164;
        world.Refresh();
        CHECK(filter->Entities() == std::vector<Entity>{entity});
    });
}

TEST_CASE("ECS-05 Late high component type updates old entity masks and existing filters", "[native][ecs][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        World world;
        const auto registry = world.GetRegistry();
        const auto filters = world.GetFiltersRegistry();
        const auto original = registry->NewEntity();
        registry->Get<BoundaryComponent<0>>(original).Value = 7;
        const auto originalFilter = filters->Get<BoundaryComponent<0>>();
        world.Refresh();
        REQUIRE(originalFilter->Entities() == std::vector<Entity>{original});

        PrimeDistinctTypes(std::make_index_sequence<70>{});
        const auto highFilter = filters->Get<BoundaryComponent<0>, BoundaryComponent<69>>();
        CHECK(highFilter->Entities().empty());
        CHECK(originalFilter->Entities() == std::vector<Entity>{original});
        const auto other = registry->NewEntity();
        world.Refresh(); // New entity must use capacity already requested by filter.
        CHECK(highFilter->Entities().empty());
        AddDistinct(registry, other, std::make_index_sequence<70>{});
        registry->Get<BoundaryComponent<69>>(original).Value = 42;
        world.Refresh();
        CheckDistinct(registry, other, std::make_index_sequence<70>{});
        CHECK(highFilter->Entities().size() == 2);
        CHECK(registry->Get<BoundaryComponent<0>>(original).Value == 7);
        CHECK(registry->Get<BoundaryComponent<69>>(original).Value == 42);
        CHECK(originalFilter->Entities().size() == 2);

        const auto excluded = filters->Get<>(Exclude<BoundaryComponent<63>>());
        CHECK(excluded->Entities() == std::vector<Entity>{original});
        const auto filterCount = filters->GetFiltersCount();
        CHECK(filters->Get<BoundaryComponent<0>>() == originalFilter);
        CHECK((filters->Get<BoundaryComponent<0>, BoundaryComponent<69>>() == highFilter));
        auto paddedInclude = Include<BoundaryComponent<0>>();
        paddedInclude.Resize(TypeId::Get<BoundaryComponent<69>>() + 64);
        CHECK(filters->GetFilter(paddedInclude, BitMask()) == originalFilter);
        CHECK(filters->GetFiltersCount() == filterCount);
        world.RefreshAll();

        registry->Del<BoundaryComponent<69>>(other);
        world.Refresh();
        CHECK(highFilter->Entities() == std::vector<Entity>{original});
        CHECK(originalFilter->Entities().size() == 2);
        registry->DestroyEntity(other);
        world.Refresh();
        const auto replacement = registry->NewEntity();
        REQUIRE(replacement.Id == other.Id);
        CHECK_FALSE(registry->Has<BoundaryComponent<69>>(replacement));
        world.Refresh();
        CHECK(highFilter->Entities() == std::vector<Entity>{original});
        CHECK(originalFilter->Entities() == std::vector<Entity>{original});
        CHECK(excluded->Entities().size() == 2);
    });
}

TEST_CASE("ECS-05 New world honors high type IDs registered in another world", "[native][ecs][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        {
            World first;
            const auto registry = first.GetRegistry();
            AddDistinct(registry, registry->NewEntity(), std::make_index_sequence<70>{});
            first.Refresh();
        }
        World second;
        const auto registry = second.GetRegistry();
        const auto entity = registry->NewEntity();
        registry->Get<BoundaryComponent<69>>(entity).Value = 913;
        second.Refresh();
        const auto filter = second.GetFiltersRegistry()->Get<BoundaryComponent<69>>();
        CHECK(filter->Entities() == std::vector<Entity>{entity});
        CHECK(registry->Get<BoundaryComponent<69>>(entity).Value == 913);
        CHECK_FALSE(registry->Has<BoundaryComponent<0>>(entity));
        registry->DestroyEntity(entity);
        second.Refresh();
        CHECK(filter->Entities().empty());
    });
}

TEST_CASE("ECS-09 Negative IDs reject without out-of-range storage access", "[native][ecs][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        const EntityId id = GENERATE(-1, -2, (std::numeric_limits<EntityId>::min)());
        World world;
        const auto registry = world.GetRegistry();
        const auto live = registry->NewEntity();
        registry->Get<BoundaryComponent<0>>(live).Value = 71;
        CheckInvalidEntityId(registry, id, live);
        world.Refresh();
        const auto filter = world.GetFiltersRegistry()->Get<BoundaryComponent<0>>();
        CHECK(filter->Entities() == std::vector<Entity>{live});
        CHECK(registry->Get<BoundaryComponent<0>>(live).Value == 71);
    });
}

TEST_CASE("ECS-09 End and huge IDs reject without corrupting live entity", "[native][ecs][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        const EntityId id = GENERATE(1, (std::numeric_limits<EntityId>::max)());
        World world;
        const auto registry = world.GetRegistry();
        const auto live = registry->NewEntity();
        registry->Get<BoundaryComponent<0>>(live).Value = 83;
        CheckInvalidEntityId(registry, id, live);
        world.Refresh();
        const auto filter = world.GetFiltersRegistry()->Get<BoundaryComponent<0>>();
        CHECK(filter->Entities() == std::vector<Entity>{live});
        CHECK(registry->Get<BoundaryComponent<0>>(live).Value == 83);
    });
}

TEST_CASE("ECS-10 Surviving registry does not call destroyed World when type count grows", "[native][ecs][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        PrimeDistinctTypes(std::make_index_sequence<70>{});
        ProtectedWorld owner;
        const auto registry = owner.Get().GetRegistry();
        const auto entity = registry->NewEntity();
        owner.Destroy();
        REQUIRE_NOTHROW(registry->Get<SurvivingRegistryComponent>(entity).Value = 19);
        CHECK(registry->Get<SurvivingRegistryComponent>(entity).Value == 19);
    });
}

TEST_CASE("ECS-10 Surviving filter registry does not refresh destroyed World", "[native][ecs][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        ProtectedWorld owner;
        const auto filters = owner.Get().GetFiltersRegistry();
        owner.Destroy();
        std::shared_ptr<Filter> filter;
        REQUIRE_NOTHROW(filter = filters->Get<SurvivingFilterComponent>());
        REQUIRE(filter != nullptr);
        CHECK(filter->Entities().empty());
    });
}

TEST_CASE("ECS-03 Proposed repeated reuse keeps every historical handle stale past second wrap", "[native][ecs][coverage][coverage-remaining][proposed-contract][isolated]")
{
    Isolated([]
    {
        // Proposed ABA protection contract; implementation may widen generation
        // or retire exhausted IDs. Oracle never requires reuse of same ID.
        constexpr u32 REUSES = 520; // Crosses both wraps of current u8 generation.
        World world;
        const auto registry = world.GetRegistry();
        std::vector<Entity> history;
        history.reserve(REUSES);
        auto current = registry->NewEntity();
        u64 revivedObservations = 0;
        u32 firstRevivalReuse = 0;
        u32 revivalAfterSecondWrap = 0;
        const auto inspectHistory = [&](const u32 reuse)
        {
            u32 aliveHistorical = 0;
            for (const auto old : history)
            {
                if (registry->IsAlive(old)) ++aliveHistorical;
            }
            revivedObservations += aliveHistorical;
            if (aliveHistorical != 0 && firstRevivalReuse == 0) firstRevivalReuse = reuse;
            if (reuse >= 512) revivalAfterSecondWrap += aliveHistorical;
        };
        for (u32 reuse = 1; reuse <= REUSES; ++reuse)
        {
            history.push_back(current); // Retain every handle from each retired or reused slot.
            registry->DestroyEntity(current);
            world.Refresh();
            inspectHistory(reuse); // Dead slots must not validate any old handle.
            current = registry->NewEntity();
            REQUIRE(current.Generation != 0);
            REQUIRE(registry->IsAlive(current));
            CHECK_FALSE(registry->Has<BoundaryComponent<0>>(current));
            registry->Get<BoundaryComponent<0>>(current).Value = static_cast<i32>(reuse);
            world.Refresh();
            inspectHistory(reuse); // Newly live entity must not revive old handles.
        }
        REQUIRE(history.size() == REUSES);
        CHECK(registry->IsAlive(current));
        CHECK(registry->Get<BoundaryComponent<0>>(current).Value == REUSES);
        CAPTURE(firstRevivalReuse, revivedObservations, revivalAfterSecondWrap, current.Id, current.Generation);
        CHECK(revivedObservations == 0);
        CHECK(revivalAfterSecondWrap == 0);
    });
}
