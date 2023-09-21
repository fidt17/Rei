#pragma once

#define ECS_WORLD(WORLD) auto _ecs = (WORLD).GetRegistry()
#define NEW_ENTITY() _ecs->NewEntity()
#define DESTROY_ENTITY(E) _ecs->DestroyEntity(E)
#define GET(E, COMPONENT_TYPE) _ecs->Get<COMPONENT_TYPE>(E)
#define HAS(E, COMPONENT_TYPE) _ecs->Has<COMPONENT_TYPE>(E)
#define DEL(E, COMPONENT_TYPE) _ecs->Del<COMPONENT_TYPE>(E)
#define IS_ALIVE(E) _ecs->IsAlive(E)
#define IS_DEAD(E) _ecs->IsDead(E)

