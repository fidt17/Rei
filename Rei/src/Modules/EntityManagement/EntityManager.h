#pragma once

namespace rei::internal
{
#ifdef REI_APP
    extern void AddBehaviourComponent(ecs::Entity e, i32 id);
#endif
}

namespace rei
{
    class EntityManager
    {
    public:
        explicit EntityManager(const std::shared_ptr<ecs::World>& world);

        REI_API ecs::Entity GetBySceneId(i32 id) const;

#ifdef REI_APP
        REI_API void AddBehaviour(ecs::Entity e, i32 componentId) const
        {
            internal::AddBehaviourComponent(e, componentId);
        }
#endif

    private:
        std::shared_ptr<ecs::World> _internalWorld;
        std::shared_ptr<ecs::Filter> _entityInfoFilter;
    };
}
