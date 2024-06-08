#pragma once

namespace rei
{
    namespace render
    {
        class Renderer;
    }

    namespace assets
    {
        class AssetManager;
    }

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
        REI_API bool EngineExists() { return _engine != nullptr; }

        void SetAssetManager(assets::AssetManager* value) { _assetManager = value; }
        REI_API assets::AssetManager& GetAssetManager() const { return *_assetManager; }

        void SetInternalWorld(ecs::World* world) { _internalWorld = world; }
        REI_API ecs::World& GetInternalWorld() const { return *_internalWorld; }
        REI_API std::shared_ptr<ecs::World> GetInternalWorldPtr() const { return std::shared_ptr<ecs::World>(_internalWorld); }

        void SetEntityManager(EntityManager* entityManager) { _entityManager = entityManager; }
        REI_API EntityManager& GetEntityManager() const { return *_entityManager; }

        void SetRenderer(render::Renderer* renderer) { _renderer = renderer; }
        REI_API render::Renderer& GetRenderer() const { return *_renderer; }

        REI_API static Services* GetInstance();

    private:
        Services() = default;
        static Services* _instance;

        internal::engine::Engine* _engine;
        assets::AssetManager* _assetManager;
        ecs::World* _internalWorld;
        render::Renderer* _renderer;
        EntityManager* _entityManager;
    };

    inline internal::engine::Engine& GetEngine() { return Services::GetInstance()->GetEngine(); }
    inline assets::AssetManager& GetAssetManager() { return Services::GetInstance()->GetAssetManager(); }
    inline ecs::World& GetInternalWorld() { return Services::GetInstance()->GetInternalWorld(); }
    inline std::shared_ptr<ecs::World> GetInternalWorldPtr() { return Services::GetInstance()->GetInternalWorldPtr(); }
    inline EntityManager& GetEntityManager() { return Services::GetInstance()->GetEntityManager(); }
}
