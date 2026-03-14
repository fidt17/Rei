#include "pch.h"
#include "EntityManager.h"

#include <algorithm>


#include "rei_behaviours/transformation/Transform.h"
#include "rei_behaviours/transformation/TransformHierarchyUtility.h"
#include "Engine/Engine.h"
#include "Engine/Services.h"
#include "Modules/Behaviour/Components/BehaviourCollection.h"
#include "Modules/Behaviour/Components/StartBehavioursEvent.h"
#include "Modules/Components/EntityInfo.h"
#include "Modules/Scenes/SceneManager.h"

namespace rei
{
    EntityManager::EntityManager(const std::shared_ptr<ecs::World>& world)
        : _ecs(world->GetRegistry()),
          _entityInfoFilter(world->GetFiltersRegistry()->Get<EntityInfo>())
    {
    }

    ecs::Entity EntityManager::GetBySceneId(const i32 id) const
    {
        FOR(e, _entityInfoFilter)
        {
            if (GET(e, EntityInfo).Id == id)
            {
                return e;
            }
        }

        return ecs::NULL_ENTITY;
    }

    ecs::Entity EntityManager::CreateNewEntity(const std::string& name)
    {
        ECS_WORLD(GetInternalWorld())

        const auto e = NEW_ENTITY();
        GET(e, EntityInfo) = {.Id = GenerateNewSceneEntityId(), .Name = name};

        auto& transform = AddBehaviour<Transform>(e);
        transform.Reset();
        
        const i32 maxRootOrder = transform_utility::GetMaxOrderForParent(ecs::NULL_ENTITY);
        transform.SetChildOrder(maxRootOrder + 1);

        return e;
    }

    void EntityManager::Create(const SceneEntity& sceneEntity) const
    {
        ECS_WORLD(GetInternalWorld())

        struct EntityToBehaviour
        {
            ecs::Entity Entity;
            i32 BehaviourId;
        };
        auto behavioursToInit = std::vector<EntityToBehaviour>();

        try
        {
            const auto e = NEW_ENTITY();
            GET(e, EntityInfo) = {.Id = sceneEntity.GetId(), .Name = sceneEntity.GetName()};
            auto& behavioursToAdd = sceneEntity.GetBehaviours();

            for (auto behaviourData : behavioursToAdd)
            {
                const i32 behaviourId = behaviourData.at("Id");

                const std::string SERIALIZE_DATA = "SerializedData";
                nlohmann::json serializedData;
                if (behaviourData.contains(SERIALIZE_DATA))
                {
                    serializedData = behaviourData.at(SERIALIZE_DATA);
                }

                AddBehaviour(e, behaviourId, serializedData, false);
                behavioursToInit.push_back({.Entity = e, .BehaviourId = behaviourId});
            }
        }
        catch (std::exception& e)
        {
            LOG_ERROR("Scene entity creation exception. Entity Id {}. Exception: {}", sceneEntity.GetId(), e.what())
        }

        for (const auto& [Entity, BehaviourId] : behavioursToInit)
        {
            auto& b = GetBehaviour(Entity, BehaviourId);
            InitBehaviour(Entity, b);
        }
    }

    Behaviour& EntityManager::GetBehaviour(const ecs::Entity e, const i32 behaviourId) const
    {
        return _behaviourRegistry.GetBehaviour(e, behaviourId);
    }

    Behaviour& EntityManager::AddBehaviour(const ecs::Entity e, const i32 behaviourId, const nlohmann::json& data, const bool init) const
    {
        auto& b = _behaviourRegistry.AddBehaviour(e, behaviourId, data);

        if (init)
        {
            InitBehaviour(e, b);
        }

        return b;
    }

    void EntityManager::DeleteBehaviour(const ecs::Entity e, const i32 behaviourId)
    {
        if (GetEngine().IsPlaymode())
        {
            GetBehaviourRegistry().GetBehaviour(e, behaviourId).Dispose();
        }

        GetBehaviourRegistry().DeleteBehaviour(e, behaviourId);
    }

    std::vector<ecs::Entity> EntityManager::GetRootEntities() const
    {
        ECS_WORLD(GetInternalWorld())

        std::vector<ecs::Entity> roots;

        FOR(e, _entityInfoFilter)
        {
            if (IS_DEAD(e) || !HAS(e, Transform)) continue;

            const auto& transform = GET(e, Transform);
            const ecs::Entity parent = transform.GetParent();
            
            if (parent != ecs::NULL_ENTITY && !IS_DEAD(parent) && HAS(parent, EntityInfo)) continue;

            roots.push_back(e);
        }

        return roots;
    }

    void EntityManager::Destroy(const ecs::Entity e) const
    {
        // recursively collects all nested entities (and root)
        auto collectEntitiesForDestroy = [&](auto&& self, const ecs::Entity current,
                                             std::vector<ecs::Entity>& result) -> void
        {
            if (IS_DEAD(current)) return;

            if (HAS(current, Transform))
            {
                for (const auto child : GET(current, Transform).GetChildren())
                {
                    self(self, child, result);
                }
            }

            result.push_back(current);
        };

        std::vector<ecs::Entity> entitiesToDestroy;
        collectEntitiesForDestroy(collectEntitiesForDestroy, e, entitiesToDestroy);

        for (const auto entity : entitiesToDestroy)
        {
            if (IS_DEAD(entity)) continue;

            if (HAS(entity, BehaviourCollection))
            {
                for (const auto behaviour : GET(entity, BehaviourCollection).Behaviours)
                {
                    if (GetEngine().IsPlaymode())
                    {
                        GetBehaviour(entity, behaviour).Dispose();
                    }
                }
            }

            DESTROY_ENTITY(entity);
        }
    }

    void EntityManager::ResolveTransformParents() const
    {
        ECS_WORLD(GetInternalWorld())

        const auto& entityInfoFilter = FILTER(EntityInfo);
        
        FOR(e, entityInfoFilter)
        {
            if (IS_DEAD(e) || !HAS(e, Transform)) continue;

            GET(e, Transform).AfterREI_SET();
        }
    }

    void EntityManager::ResolveDependencies() const
    {
        ECS_WORLD(GetInternalWorld())

        const auto& entityInfoFilter = FILTER(EntityInfo);

        FOR(e, entityInfoFilter)
        {
            if (IS_DEAD(e) || !HAS(e, BehaviourCollection)) continue;

            for (const auto behaviourId : GET(e, BehaviourCollection).Behaviours)
            {
                _behaviourRegistry.ResolveBehaviourDependencies(e, behaviourId);
            }
        }
    }

    ecs::Entity EntityManager::Instantiate(const ecs::Entity source, const std::string& requestedName, const bool includeChildren) const
    {
        ECS_WORLD(GetInternalWorld())

        if (IS_DEAD(source) || !HAS(source, EntityInfo) || !HAS(source, Transform))
        {
            REI_THROW("Cannot instantiate from NULL entity, source={}, requestedName={}", std::string(source), requestedName)
        }

        // json work-around
        auto wrapValueForSet = [&](const nlohmann::json& value, auto&& wrapValueForSetRef) -> nlohmann::json
        {
            if (value.is_object())
            {
                nlohmann::json obj = nlohmann::json::object();
                for (const auto& item : value.items())
                {
                    if (item.key() == "REI_TYPE")
                    {
                        obj[item.key()] = item.value();
                    }
                    else
                    {
                        obj[item.key()] = wrapValueForSetRef(item.value(), wrapValueForSetRef);
                    }
                }

                return nlohmann::json { { "Value", obj } };
            }

            return nlohmann::json { { "Value", value } };
        };

        // recursively creates entities and required children
        auto instantiateRecursive = [&](auto&& self, const ecs::Entity src, const std::string& nameOverride,
                                        const ecs::Entity parent, const i32 order) -> ecs::Entity
        {
            if (IS_DEAD(src) || !HAS(src, EntityInfo) || !HAS(src, Transform))
            {
                REI_THROW("Cannot instantiate from NULL entity, source={}, nameOverride={}", std::string(src), nameOverride)
            }

            const auto srcName = GET(src, EntityInfo).Name;
            const auto srcLocalPosition = GET(src, Transform).GetLocalPosition();
            const auto srcLocalScale = GET(src, Transform).GetLocalScale();
            const auto srcLocalRotation = GET(src, Transform).GetLocalRotation();
            const auto srcBehaviourIds = GET(src, BehaviourCollection).Behaviours;
            const i32 transformBehaviourId = _behaviourRegistry.GetId<Transform>();
            auto srcChildren = includeChildren ? GET(src, Transform).GetChildren() : std::vector<ecs::Entity>();

            const auto clone = NEW_ENTITY();
            GET(clone, EntityInfo) = {.Id = GenerateNewSceneEntityId(), .Name = nameOverride.empty() ? srcName : nameOverride};

            auto& transform = AddBehaviour<Transform>(clone);
            transform.Reset();
            transform.GetLocalPosition() = srcLocalPosition;
            transform.GetLocalScale() = srcLocalScale;
            transform.SetRotation(srcLocalRotation);
            transform_utility::InsertWithOrder(transform, parent, order);

            for (const auto behaviourId : srcBehaviourIds)
            {
                if (behaviourId == transformBehaviourId) continue;

                const auto behaviourData = _behaviourRegistry.GetBehaviourData(src, behaviourId);
                nlohmann::json behaviourSetData = nlohmann::json::object();

                for (const auto& item : behaviourData.items())
                {
                    if (item.key() == "REI_TYPE")
                    {
                        behaviourSetData[item.key()] = item.value();
                    }
                    else
                    {
                        behaviourSetData[item.key()] = wrapValueForSet(item.value(), wrapValueForSet);
                    }
                }

                AddBehaviour(clone, behaviourId, behaviourSetData, true);
            }

            if (includeChildren)
            {
                std::ranges::sort(srcChildren, [&](const ecs::Entity& a, const ecs::Entity& b)
                {
                    const auto& aTransform = GET(a, Transform);
                    const auto& bTransform = GET(b, Transform);
                    return aTransform.GetChildOrder() < bTransform.GetChildOrder();
                });

                for (const auto child : srcChildren)
                {
                    const auto& childTransform = GET(child, Transform);
                    self(self, child, "", clone, childTransform.GetChildOrder());
                }
            }

            return clone;
        };

        const auto& sourceTransform = GET(source, Transform);

        return instantiateRecursive(instantiateRecursive,
            source,
            requestedName,
            sourceTransform.GetParent(),
            sourceTransform.GetChildOrder() + 1);
    }

    void EntityManager::InitBehaviour(const ecs::Entity e, Behaviour& b) const
    {
        b.LoadAssets(GetAssetManager());
        b.Init();

        GET(e, StartBehavioursEvent).Behaviours.push_back(b.GetBehaviourId());
    }

    i32 EntityManager::GenerateNewSceneEntityId() const
    {
        GetInternalWorld()->RefreshAll(); // force refresh filters in case we are creating multiple entities in one frame
        
        i32 maxId = -1;
        FOR(e, _entityInfoFilter)
        {
            const i32 id = GET(e, EntityInfo).Id;
            maxId = max(id, maxId);
        }

        return maxId + 1;
    }

}
