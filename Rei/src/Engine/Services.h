#pragma once

namespace rei::assets
{
    class AssetManager;
}

namespace rei
{
    class Services
    {
    public:
        Services(Services& other) = delete;
        void operator=(const Services&) = delete;

        void SetAssetManager(assets::AssetManager* value) { _assetManager = value; }
        REI_API assets::AssetManager& GetAssetManager() const { return *_assetManager; }

        void SetInternalWorld(ecs::World* world) { _internalWorld = world; }
        REI_API ecs::World& GetInternalWorld() const { return *_internalWorld; }

        REI_API static Services* GetInstance();

    private:
        Services() = default;
        static Services* _instance;

        assets::AssetManager* _assetManager;
        ecs::World* _internalWorld;
    };

    inline assets::AssetManager& GetAssetManager() { return Services::GetInstance()->GetAssetManager(); }
    inline ecs::World& GetInternalWorld() { return Services::GetInstance()->GetInternalWorld(); }
}
