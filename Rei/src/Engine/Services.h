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
        
        REI_API static Services* GetInstance();

    private:
        Services() = default;
        static Services* _instance;

        assets::AssetManager* _assetManager;
    };

    inline assets::AssetManager& GetAssetManager() { return Services::GetInstance()->GetAssetManager(); }
}
