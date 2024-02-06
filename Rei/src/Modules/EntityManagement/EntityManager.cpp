#include "pch.h"
#include "EntityManager.h"

#include "Modules/Behaviour/Components/BehaviourCollection.h"
#include "Modules/Behaviour/Components/StartBehavioursCommand.h"
#include "Modules/Components/EntityInfo.h"

rei::EntityManager::EntityManager(const std::shared_ptr<ecs::World>& world)
    : _internalWorld(world),
      _entityInfoFilter(world->GetFiltersRegistry()->Get<EntityInfo>())
{
}

rei::ecs::Entity rei::EntityManager::GetBySceneId(const i32 id) const
{
    for (const auto e : *_entityInfoFilter)
    {
        if (_internalWorld->GetRegistry()->Get<EntityInfo>(e).Id == id)
        {
            return e;
        }
    }

    return ecs::NULL_ENTITY;
}

rei::Behaviour& rei::EntityManager::GetBehaviour(const ecs::Entity e, const i32 componentId) const
{
    return _behaviourRegistry.GetBehaviour(e, componentId);
}

rei::Behaviour& rei::EntityManager::AddBehaviour(const ecs::Entity e, const i32 componentId, const nlohmann::json& data, const bool init) const
{
    ECS_WORLD(*_internalWorld);
    auto& b = _behaviourRegistry.AddBehaviour(e, componentId, data);

    GET(e, BehaviourCollection).Behaviours.push_back(componentId);

    if (init)
    {
        InitBehaviour(e, b);
    }

    return b;
}

void rei::EntityManager::InitBehaviour(const ecs::Entity e, Behaviour& b) const
{
    ECS_WORLD(*_internalWorld);
    b.Init();
    GET(e, StartBehavioursCommand).Behaviours.push_back(b.GetId());
}
