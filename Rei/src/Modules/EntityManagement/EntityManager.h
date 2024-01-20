#pragma once
#include "Engine/Services.h"

namespace rei
{
    class MyB : public Behaviour
    {
    public:
        void Init() override {}
    };
    
    class BehaviourComponentFactory
    {
    public:
        template <typename T>
        void RegisterComponent(i32 id)
        {
            _factoryMethods.insert({id, [](ecs::Entity e) -> T&
            {
                ECS_WORLD(GetInternalWorld());
                return GET(e, T);
            }});
        }

        Behaviour& AddBehaviour(const ecs::Entity e, const i32 id) const
        {
            if (_factoryMethods.count(id) == 0) REI_THROW("Missing component factory. Component ID: " + STRING(id))

            return _factoryMethods.at(id)(e);
        }

    private:
        std::unordered_map<i32, std::function<Behaviour& (ecs::Entity)>> _factoryMethods{};
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

        BehaviourComponentFactory _componentFactory;
    private:
        std::shared_ptr<ecs::World> _internalWorld;
        std::shared_ptr<ecs::Filter> _entityInfoFilter;
    };
}
