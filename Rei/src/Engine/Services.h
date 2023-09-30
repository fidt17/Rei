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

        void SetAssetManager(const std::shared_ptr<assets::AssetManager>& value) { _assetManager = value; }
        REI_API std::shared_ptr<assets::AssetManager> GetAssetManager() const { return _assetManager; }
        
        static Services* GetInstance();

    private:
        Services() = default;
        static Services* _instance;

        std::shared_ptr<assets::AssetManager> _assetManager;
    };

    Services* Services::_instance = nullptr;

    inline Services* Services::GetInstance()
    {
        if (_instance == nullptr)
        {
            _instance = new Services();
        }
        return _instance;
    }

    inline const assets::AssetManager* GetAssetManager() { return Services::GetInstance()->GetAssetManager().get(); }
}
