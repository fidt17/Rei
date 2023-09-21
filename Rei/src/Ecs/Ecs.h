#pragma once

#define ECS_WORLD(WORLD) auto __ecs = (WORLD).GetRegistry()
#define NEW_ENTITY() __ecs->NewEntity()
#define DESTROY_ENTITY(E) __ecs->DestroyEntity(E)
#define GET(E, COMPONENT_TYPE) __ecs->Get<COMPONENT_TYPE>(E)
#define HAS(E, COMPONENT_TYPE) __ecs->Has<COMPONENT_TYPE>(E)
#define DEL(E, COMPONENT_TYPE) __ecs->Del<COMPONENT_TYPE>(E)
#define IS_ALIVE(E) __ecs->IsAlive(E)
#define IS_DEAD(E) __ecs->IsDead(E)

