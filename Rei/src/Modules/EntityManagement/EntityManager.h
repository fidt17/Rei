#pragma once
#include <typeindex>

#include "Engine/Services.h"
#include "Modules/Scenes/SceneEntity.h"

namespace rei
{
    class BehaviourRegistry
    {
    public:
        template <typename T>
        void RegisterComponent(i32 id)
        {
            _addMethods.insert({
                id, [=](const ecs::Entity e, const nlohmann::json& data) -> T& {
                    ECS_WORLD(GetInternalWorld());
                    T& t = GET(e, T);

                    if (data.empty())
                    {
                        t = T(id, e);
                    }
                    else
                    {
                        t = T(id, e, data);
                    }

                    return t;
                }
            });

            _getMethods.insert({
                id, [](const ecs::Entity e) -> T& {
                    ECS_WORLD(GetInternalWorld());
                    return GET(e, T);
                }
            });

            _behaviourIdMap[std::type_index(typeid(T))] = id;
        }

        Behaviour& AddBehaviour(const ecs::Entity e, const i32 id, const nlohmann::json& data) const
        {
            if (_addMethods.count(id) == 0)
                REI_THROW("Missing component factory. Component ID: " + STRING(id))

            return _addMethods.at(id)(e, data);
        }

        Behaviour& GetBehaviour(const ecs::Entity e, const i32 id) const
        {
            if (_getMethods.count(id) == 0)
                REI_THROW("Missing component factory. Component ID: " + STRING(id))

            return _getMethods.at(id)(e);
        }

        template <typename R>
        i32 GetId()
        {
            return _behaviourIdMap[std::type_index(typeid(R))];
        }

    private:
        std::unordered_map<i32, std::function<Behaviour&(ecs::Entity, const nlohmann::json&)>> _addMethods{};
        std::unordered_map<i32, std::function<Behaviour&(ecs::Entity)>> _getMethods{};
        std::map<std::type_index, i32> _behaviourIdMap;
    };

    class EntityManager
    {
    public:
        explicit EntityManager(const std::shared_ptr<ecs::World>& world);

        REI_API ecs::Entity GetBySceneId(i32 id) const;

        REI_API void Create(const SceneEntity& sceneEntity) const;

        REI_API Behaviour& GetComponent(ecs::Entity e, i32 componentId) const;
        REI_API Behaviour& AddComponent(ecs::Entity e, i32 componentId, const nlohmann::json& data, bool init = true) const;

        template <typename T>
        REI_API T& AddComponent(ecs::Entity e)
        {
            const i32 id = _behaviourRegistry.GetId<T>();
            return static_cast<T&>(AddComponent(e, id, nlohmann::json()));
        }

        REI_API void Destroy(ecs::Entity e) const;

        void InitBehaviour(ecs::Entity e, Behaviour& b) const;

        BehaviourRegistry& GetBehaviourRegistry() { return _behaviourRegistry; }

    private:
        BehaviourRegistry _behaviourRegistry;

        std::shared_ptr<ecs::EcsRegistry> _ecs;
        std::shared_ptr<ecs::Filter> _entityInfoFilter;
    };
}

#define ADD_BEHAVIOUR(e, T) rei::GetEntityManager().AddComponent<T>(e)