#include "pch.h"
#include "EntityManager.h"

#include <algorithm>
#include <unordered_set>


#include "rei_behaviours/transformation/Transform.h"
#include "Common/Transform/TransformHierarchyUtility.h"
#include "Engine/Engine.h"
#include "Engine/Services.h"
#include "Modules/Behaviour/Components/BehaviourCollection.h"
#include "Modules/Behaviour/Components/StartBehavioursEvent.h"
#include "Modules/Components/EntityInfo.h"
#include "Modules/Scenes/SceneManager.h"

namespace rei
{
    namespace
    {
        // Setters wrap object fields; collection elements keep their own shape.
        nlohmann::json WrapBehaviourProperties(const nlohmann::json& value)
        {
            if (value.is_array())
            {
                auto items = nlohmann::json::array();
                for (const auto& item : value)
                {
                    items.push_back(WrapBehaviourProperties(item));
                }
                return items;
            }
            if (!value.is_object()) return value;

            auto object = nlohmann::json::object();
            for (const auto& item : value.items())
            {
                object[item.key()] = item.key() == "REI_TYPE" ? item.value() :
                    nlohmann::json{{"Value", WrapBehaviourProperties(item.value())}};
            }
            return object;
        }
    }

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

    ecs::Entity EntityManager::Create(const SceneEntity& sceneEntity) const
    {
        ECS_WORLD(GetInternalWorld())

        const auto entity = NEW_ENTITY();
        GET(entity, EntityInfo) = {.Id = sceneEntity.GetId(), .Name = sceneEntity.GetName()};
        _lastAllocatedSceneEntityId = max(_lastAllocatedSceneEntityId, sceneEntity.GetId());
        for (const auto& behaviourData : sceneEntity.GetBehaviours())
        {
            const i32 behaviourId = behaviourData.at("Id");
            const auto serializedData = behaviourData.value("SerializedData", nlohmann::json());
            if (HasBehaviour(entity, behaviourId))
            {
                _behaviourRegistry.SetBehaviourData(entity, behaviourId, serializedData);
            }
            else
            {
                AddBehaviour(entity, behaviourId, serializedData, false);
            }
        }

        if (!HAS(entity, BehaviourCollection)) return entity;
        const auto behavioursToInit = GET(entity, BehaviourCollection).Behaviours;
        for (const auto behaviourId : behavioursToInit)
        {
            InitBehaviour(entity, GetBehaviour(entity, behaviourId));
        }
        return entity;
    }

    SceneEntity EntityManager::CaptureSceneEntity(const ecs::Entity entity) const
    {
        const auto& info = GET(entity, EntityInfo);
        auto behaviours = nlohmann::json::array();
        for (const auto id : GET(entity, BehaviourCollection).Behaviours)
        {
            behaviours.push_back({{"Id", id}, {"SerializedData", WrapBehaviourProperties(_behaviourRegistry.GetBehaviourData(entity, id))}});
        }
        return SceneEntity({{"Id", info.Id}, {"Name", info.Name}, {"Behaviours", std::move(behaviours)}});
    }

    Behaviour& EntityManager::GetBehaviour(const ecs::Entity e, const i32 behaviourId) const
    {
        return _behaviourRegistry.GetBehaviour(e, behaviourId);
    }

    Behaviour& EntityManager::AddBehaviour(const ecs::Entity e, const i32 behaviourId, const nlohmann::json& data, const bool init) const
    {
        for (const auto requiredBehaviourId : _behaviourRegistry.GetRequiredBehaviourIds(behaviourId))
        {
            if (HasBehaviour(e, requiredBehaviourId)) continue;

            AddBehaviour(e, requiredBehaviourId, nlohmann::json(), init);
        }

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
        const auto isDeadOrPendingDestroy = [&](const ecs::Entity entity)
        {
            return IS_DEAD(entity) || _ecs->GetDestroyedEntities().contains(entity);
        };
        // Collect children before parents, including malformed cyclic hierarchies.
        std::unordered_set<ecs::Entity> visited;
        auto collectEntitiesForDestroy = [&](auto&& self, const ecs::Entity current,
                                             std::vector<ecs::Entity>& result) -> void
        {
            if (isDeadOrPendingDestroy(current) || !visited.insert(current).second) return;

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
            if (isDeadOrPendingDestroy(entity)) continue;
            // ECS keeps components readable until Refresh; mark first to prevent repeated or reentrant Dispose.
            DESTROY_ENTITY(entity);

            if (HAS(entity, BehaviourCollection) && GetEngine().IsPlaymode())
            {
                const auto behaviours = GET(entity, BehaviourCollection).Behaviours;
                for (const auto behaviour : behaviours)
                {
                    if (IS_DEAD(entity)) break;
                    if (!HasBehaviour(entity, behaviour)) continue;
                    try
                    {
                        GetBehaviour(entity, behaviour).Dispose();
                    }
                    catch (const std::exception& exception)
                    {
                        LOG_ERROR("Behaviour disposal failed. Entity {}, behaviour {}. {}", std::string(entity), behaviour, exception.what())
                    }
                    catch (...)
                    {
                        LOG_ERROR("Behaviour disposal failed. Entity {}, behaviour {}. Unknown exception", std::string(entity), behaviour)
                    }
                }
            }

        }
    }

    void EntityManager::DestroySceneEntities() const
    {
        const auto world = GetInternalWorld();
        world->Refresh();
        const auto entities = _entityInfoFilter->Entities();
        for (const auto entity : entities)
        {
            Destroy(entity);
        }
        world->Refresh();
        _lastAllocatedSceneEntityId = 0;
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
                const auto behaviourSetData = WrapBehaviourProperties(behaviourData);
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
        _behaviourRegistry.ResolveBehaviourDependencies(e, b.GetBehaviourId());
        b.LoadAssets(GetAssetManager());
        b.Init();

        GET(e, StartBehavioursEvent).Behaviours.push_back(b.GetBehaviourId());
    }

    i32 EntityManager::GenerateNewSceneEntityId() const
    {
        GetInternalWorld()->RefreshAll(); // force refresh filters in case we are creating multiple entities in one frame
        
        i32 maxId = _lastAllocatedSceneEntityId; // Serialized references reserve zero for null.
        FOR(e, _entityInfoFilter)
        {
            const i32 id = GET(e, EntityInfo).Id;
            maxId = max(id, maxId);
        }

        return _lastAllocatedSceneEntityId = maxId + 1;
    }

    bool EntityManager::HasBehaviour(const ecs::Entity e, const i32 behaviourId) const
    {
        if (!HAS(e, BehaviourCollection)) return false;

        const auto& behaviours = GET(e, BehaviourCollection).Behaviours;
        return std::ranges::find(behaviours, behaviourId) != behaviours.end();
    }

}
