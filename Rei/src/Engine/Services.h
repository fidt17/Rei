#pragma once

namespace rei
{
    class EntityManager;

    namespace internal::engine
    {
        class Engine;
    }

    class Services
    {
    public:
        Services(Services& other) = delete;
        void operator=(const Services&) = delete;

        void SetEngine(internal::engine::Engine* value) { _engine = value; }
        REI_API internal::engine::Engine& GetEngine() const { return *_engine; }

        void SetInternalWorld(const std::shared_ptr<ecs::World>& world) { _internalWorld = world; }
        REI_API ecs::World& GetInternalWorld() const { return *_internalWorld; }

        void SetEntityManager(const std::shared_ptr<EntityManager>& entityManager) { _entityManager = entityManager; }
        REI_API EntityManager& GetEntityManager() const { return *_entityManager; }

        REI_API static Services* GetInstance();

    private:
        Services() = default;
        static Services* _instance;

        internal::engine::Engine* _engine;
        std::shared_ptr<ecs::World> _internalWorld;
        std::shared_ptr<EntityManager> _entityManager;
    };

    inline internal::engine::Engine& GetEngine() { return Services::GetInstance()->GetEngine(); }
    inline ecs::World& GetInternalWorld() { return Services::GetInstance()->GetInternalWorld(); }
    inline EntityManager& GetEntityManager() { return Services::GetInstance()->GetEntityManager(); }
}
