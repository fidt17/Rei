#pragma once

namespace rei::assets
{
    class AssetManager;
}

namespace rei
{
    class EntityManager;

    class Services
    {
    public:
        Services(Services& other) = delete;
        void operator=(const Services&) = delete;

        void SetAssetManager(assets::AssetManager* value) { _assetManager = value; }
        REI_API assets::AssetManager& GetAssetManager() const { return *_assetManager; }

        void SetInternalWorld(ecs::World* world) { _internalWorld = world; }
        REI_API ecs::World& GetInternalWorld() const { return *_internalWorld; }
        REI_API std::shared_ptr<ecs::World> GetInternalWorldPtr() const { return std::shared_ptr<ecs::World>(_internalWorld); }

        void SetEntityManager(EntityManager* entityManager) { _entityManager = entityManager; }
        REI_API EntityManager& GetEntityManager() const { return *_entityManager; }

        REI_API static Services* GetInstance();

    private:
        Services() = default;
        static Services* _instance;

        assets::AssetManager* _assetManager;
        ecs::World* _internalWorld;
        EntityManager* _entityManager;
    };

    inline assets::AssetManager& GetAssetManager() { return Services::GetInstance()->GetAssetManager(); }
    inline ecs::World& GetInternalWorld() { return Services::GetInstance()->GetInternalWorld(); }
    inline std::shared_ptr<ecs::World> GetInternalWorldPtr() { return Services::GetInstance()->GetInternalWorldPtr(); }
    inline EntityManager& GetEntityManager() { return Services::GetInstance()->GetEntityManager(); }
}
