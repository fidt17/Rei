#pragma once

#include "World.h"
#include "EcsRegistry.h"
#include "FiltersRegistry.h"
#include "Filter.h"
#include "BitMask.h"

#define ECS_WORLD(WORLD) auto _ecs = (WORLD)->GetRegistry(); auto _ecsWorld = WORLD;
#define NEW_ENTITY() _ecs->NewEntity()
#define DESTROY_ENTITY(E) _ecs->DestroyEntity(E)
#define GET(E, COMPONENT_TYPE) _ecs->Get<COMPONENT_TYPE>(E)
#define GET_REF(E, COMPONENT_TYPE) rei::ecs::RefComponent<COMPONENT_TYPE>(_ecs, E)
#define HAS(E, COMPONENT_TYPE) _ecs->Has<COMPONENT_TYPE>(E)
#define DEL(E, COMPONENT_TYPE) _ecs->Del<COMPONENT_TYPE>(E)
#define IS_ALIVE(E) _ecs->IsAlive(E)
#define IS_DEAD(E) _ecs->IsDead(E)

#define ENABLE(E) GET(E, rei::ActiveTag);
#define DISABLE(E) DEL(E, rei::ActiveTag);
#define IS_ACTIVE(E) HAS(E, rei::ActiveTag)

#define FILTER(...) _ecsWorld->GetFiltersRegistry()->Get<__VA_ARGS__>()
#define FILTER_MASK(INCLUDE_MASK, EXCLUDE_MASK) _ecsWorld->GetFiltersRegistry()->GetFilter(INCLUDE_MASK, EXCLUDE_MASK)
#define INCLUDE(...) rei::ecs::Include<__VA_ARGS__>()
#define EXCLUDE(...) rei::ecs::Exclude<__VA_ARGS__>()

#define FIND(...) FILTER(__VA_ARGS__)->First();

#define FOR(e, f) REI_ASSERT_NOT_NULL(f);\
            for (const auto (e) : *(f))
