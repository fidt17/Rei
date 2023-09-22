#include "pch.h"
#include "catch_amalgamated.hpp"
#include "Ecs/FiltersRegistry.h"
#include "Ecs/World.h"

using namespace rei::ecs;

struct C1
{
    int Value;
};

struct C2
{
    int Value;
};

struct C3
{
    int Value;
};

TEST_CASE("Add Single Component")
{
    World w;
    ECS_WORLD(w);
    const auto r = w.GetRegistry();
    const auto e = NEW_ENTITY();

    GET(e, C1) = C1{7};

    REQUIRE(HAS(e, C1));
    REQUIRE(GET(e, C1).Value == 7);
}

TEST_CASE("Add Multiple Components")
{
    World w;
    ECS_WORLD(w);
    const auto e = NEW_ENTITY();
    GET(e, C1) = C1{7};
    GET(e, C2) = C2{14};

    REQUIRE(GET(e, C1).Value == 7);
    REQUIRE(GET(e, C2).Value == 14);
}

TEST_CASE("Delete One Component")
{
    World w;
    ECS_WORLD(w);
    const auto e = NEW_ENTITY();

    GET(e, C1);
    REQUIRE(HAS(e, C1));
    DEL(e, C1);
    REQUIRE(!HAS(e, C1));
}

TEST_CASE("Delete Multiple Components")
{
    World w;
    ECS_WORLD(w);
    auto e = NEW_ENTITY();

    GET(e, C1);
    GET(e, C2);
    REQUIRE(HAS(e, C1));
    REQUIRE(HAS(e, C2));

    DEL(e, C1);
    REQUIRE(!HAS(e, C1));
    REQUIRE(HAS(e, C2));

    GET(e, C1);
    DEL(e, C2);
    REQUIRE(HAS(e, C1));
    REQUIRE(!HAS(e, C2));
}

TEST_CASE("Empty Filter")
{
    World w;
    ECS_WORLD(w);
    const auto f = w.GetFiltersRegistry()->NewFilter();

    NEW_ENTITY();
    REQUIRE(f->Entities().empty());
}

TEST_CASE("Single Include Filter")
{
    World w;
    ECS_WORLD(w);
    const auto f = w.GetFiltersRegistry()->NewFilter()->Include<C1>();

    const auto e = NEW_ENTITY();
    REQUIRE(f->Entities().empty());

    GET(e, C1);
    w.Refresh();

    REQUIRE(f->Entities().size() == 1);
}

TEST_CASE("Multiple Include Filter")
{
    World w;
    ECS_WORLD(w);
    const auto f = w.GetFiltersRegistry()->NewFilter()->Include<C1, C2, C3>();

    const auto e = NEW_ENTITY();
    REQUIRE(f->Entities().empty());

    GET(e, C1);
    w.Refresh();

    REQUIRE(f->Entities().empty());

    GET(e, C2);
    w.Refresh();

    REQUIRE(f->Entities().empty());

    GET(e, C3);
    w.Refresh();

    REQUIRE(f->Entities().size() == 1);
}

TEST_CASE("Multiple Include & Exclude Filter")
{
    World w;
    ECS_WORLD(w);
    const auto f = w.GetFiltersRegistry()->NewFilter()->Include<C1, C2, C3>()->Exclude<C3>();

    const auto e = NEW_ENTITY();
    REQUIRE(f->Entities().empty());

    GET(e, C1);
    w.Refresh();

    REQUIRE(f->Entities().empty());

    GET(e, C2);
    w.Refresh();

    REQUIRE(f->Entities().size() == 1);

    GET(e, C3);
    w.Refresh();

    REQUIRE(f->Entities().empty());
}

TEST_CASE("Destroyed entity gets removed from filters")
{
    World w;
    ECS_WORLD(w);

    const auto f = w.GetFiltersRegistry()->NewFilter()->Include<C1>();
    const auto e = NEW_ENTITY();

    GET(e, C1);
    w.Refresh();

    REQUIRE(!f->Entities().empty());

    DESTROY_ENTITY(e);
    w.Refresh();

    REQUIRE(f->Entities().empty());
}

TEST_CASE("Destroyed entity Id is reserved for future entities")
{
    World w;
    ECS_WORLD(w);

    auto e0 = NEW_ENTITY();
    GET(e0, C1);
    REQUIRE((e0.Id == 0 && e0.Generation == 1));

    auto e1 = NEW_ENTITY();
    REQUIRE((e1.Id == 1 && e1.Generation == 1));

    DESTROY_ENTITY(e0);
    w.Refresh();

    REQUIRE(w.GetRegistry()->GetEntityById(e0.Id).Generation == 0);

    auto e2 = NEW_ENTITY();
    REQUIRE((e2.Id == e0.Id && e2.Generation == 2));
    REQUIRE(!HAS(e2, C1));

    auto e3 = NEW_ENTITY();
    REQUIRE((e3.Id == 2 && e3.Generation == 1));
}

TEST_CASE("Destroyed entity is marked as dead")
{
    World w;
    ECS_WORLD(w);
    const auto e = NEW_ENTITY();

    REQUIRE(IS_ALIVE(e));

    DESTROY_ENTITY(e);
    w.Refresh();

    REQUIRE(IS_DEAD(e));
}

TEST_CASE("Cannot get component on dead entity")
{
    World w;
    ECS_WORLD(w);
    const auto e = NEW_ENTITY();

    DESTROY_ENTITY(e);
    w.Refresh();

    LOGGER_DISABLE()
    REQUIRE_THROWS(GET(e, C1));
    LOGGER_ENABLE()
}

TEST_CASE("Cannot delete component on dead entity")
{
    World w;
    ECS_WORLD(w);
    const auto e = NEW_ENTITY();
    DESTROY_ENTITY(e);
    w.Refresh();

    LOGGER_DISABLE()
    REQUIRE_THROWS(DEL(e, C1));
    LOGGER_ENABLE()
}

TEST_CASE("Cannot check if dead entity has component")
{
    World w;
    ECS_WORLD(w);
    const auto e = NEW_ENTITY();
    DESTROY_ENTITY(e);
    w.Refresh();

    LOGGER_DISABLE()
    REQUIRE_THROWS(HAS(e, C1));
    LOGGER_ENABLE()
}

TEST_CASE("Cannot get mask of dead entity")
{
    World w;
    ECS_WORLD(w);
    const auto ecs = w.GetRegistry();
    const auto e = NEW_ENTITY();

    DESTROY_ENTITY(e);
    w.Refresh();

    LOGGER_DISABLE()
    REQUIRE_THROWS(ecs->GetEntityMask(e));
    LOGGER_ENABLE()
}

TEST_CASE("Counter system")
{
    struct Counter
    {
        int Value;
    };

    class CounterSystem : public System
    {
    public:
        CounterSystem(const std::shared_ptr<EcsRegistry>& ecs, const std::shared_ptr<FiltersRegistry>& filtersRegistry, const int step)
            : System(ecs, filtersRegistry), _step(step)
        {
            _filter = filtersRegistry->NewFilter()->Include<Counter>();
        }

        void OnUpdate() override
        {
            FOR(_filter)
            {
                GET(e, Counter).Value += _step;
            }
        }

    private:
        std::shared_ptr<Filter> _filter;
        int _step;
    };

    World w;
    ECS_WORLD(w);

    w.AddSystem<CounterSystem>(2);

    auto e = NEW_ENTITY();
    GET(e, Counter);

    w.Refresh();
    for (int i = 0; i < 100; i++)
    {
        w.Run();
    }

    REQUIRE(GET(e, Counter).Value == 200);
}

TEST_CASE("Entity Creation Destruction Systems")
{
    struct Counter
    {
        int CreatedEntities;
        int DestroyedEntities;
    };
    
    struct DestroyEntityEvent { };

    class EntityCreationSystem : public System
    {
    public:
        EntityCreationSystem(const std::shared_ptr<EcsRegistry>& ecs, const std::shared_ptr<FiltersRegistry>& filtersRegistry)
            : System(ecs, filtersRegistry)
        {
            _counterFilter = filtersRegistry->NewFilter()->Include<Counter>();
        }

        void OnUpdate() override
        {
            const auto e = NEW_ENTITY();
            GET(e, DestroyEntityEvent);

            FOR(_counterFilter)
            {
                GET(e, Counter).CreatedEntities += 1;
            }
        }
    private:
        std::shared_ptr<Filter> _counterFilter;
    };

    class HandleDestroyEntityEventSystem : public System
    {
    public:
        HandleDestroyEntityEventSystem(const std::shared_ptr<EcsRegistry>& ecs, const std::shared_ptr<FiltersRegistry>& filtersRegistry)
            : System(ecs, filtersRegistry)
        {
            _destroyFilter = filtersRegistry->NewFilter()->Include<DestroyEntityEvent>();
            _counterFilter = filtersRegistry->NewFilter()->Include<Counter>();
        }

        void OnUpdate() override
        {
            FOR(_destroyFilter)
            {
                DESTROY_ENTITY(e);

                for (const auto e1 : *(_counterFilter))
                {
                    GET(e1, Counter).DestroyedEntities += 1;
                }
            }
        }
        
    private:
        std::shared_ptr<Filter> _destroyFilter;
        std::shared_ptr<Filter> _counterFilter;
    };

    World w;
    ECS_WORLD(w);
    w.AddSystem<EntityCreationSystem>();
    w.AddSystem<HandleDestroyEntityEventSystem>();

    const auto counterEntity = NEW_ENTITY();
    GET(counterEntity, Counter);
    
    w.Refresh();
    for (int i = 0; i < 100; i++)
    {
        w.Run();
    }

    REQUIRE(GET(counterEntity, Counter).CreatedEntities == 100);
    REQUIRE(GET(counterEntity, Counter).DestroyedEntities == 100);
}
