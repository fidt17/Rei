#pragma once
#include "Engine/Services.h"

namespace rei
{
    class BehaviourComponentFactory
    {
    public:
        template <typename T>
        void RegisterComponent(i32 id)
        {
            _factoryMethods.insert({
                id, [=](const ecs::Entity e, const nlohmann::json& data) -> T&
                {
                    ECS_WORLD(GetInternalWorld());
                    T& t = GET(e, T);
                    t = T(e, data);
                    return t;
                }
            });
            
            _factoryMethodsWithoutSerialization.insert({
                id, [](const ecs::Entity e) -> T&
                {
                    ECS_WORLD(GetInternalWorld());
                    return GET(e, T);
                }
            });
        }

        Behaviour& AddBehaviour(const ecs::Entity e, const i32 id, const nlohmann::json& data) const
        {
            if (_factoryMethods.count(id) == 0)
                REI_THROW("Missing component factory. Component ID: " + STRING(id))

            return _factoryMethods.at(id)(e, data);
        }
        
        Behaviour& AddBehaviour(const ecs::Entity e, const i32 id) const
        {
            if (_factoryMethodsWithoutSerialization.count(id) == 0)
                REI_THROW("Missing component factory. Component ID: " + STRING(id))

            return _factoryMethodsWithoutSerialization.at(id)(e);
        }

    private:
        std::unordered_map<i32, std::function<Behaviour&(ecs::Entity, const nlohmann::json&)>> _factoryMethods{};
        std::unordered_map<i32, std::function<Behaviour&(ecs::Entity)>> _factoryMethodsWithoutSerialization{};
    };

    class EntityManager
    {
    public:
        explicit EntityManager(const std::shared_ptr<ecs::World>& world);

        REI_API ecs::Entity GetBySceneId(i32 id) const;
        
        REI_API Behaviour& AddBehaviour(const ecs::Entity e, const i32 componentId) const
        {
            return _componentFactory.AddBehaviour(e, componentId);
        }

        Behaviour& AddBehaviour(const ecs::Entity e, const i32 componentId, const nlohmann::json& data) const
        {
            return _componentFactory.AddBehaviour(e, componentId, data);
        }

        BehaviourComponentFactory _componentFactory;

    private:
        std::shared_ptr<ecs::World> _internalWorld;
        std::shared_ptr<ecs::Filter> _entityInfoFilter;
    };
}
