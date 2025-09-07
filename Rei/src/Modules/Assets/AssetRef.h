#pragma once

namespace rei::assets
{
    class IAssetRef
    {
    public:
        virtual ~IAssetRef() = default;

        virtual void UnloadAsset() = 0;
        virtual i32 GetAssetSize() = 0;
    };
    
    template <typename T>
    struct AssetRef : public IAssetRef
    {
        SERIALIZABLE_BODY(AssetRef)
        
        SERIALIZE std::string Id;
        
        T* Asset;
        bool IsLoaded = false;
        i32 AssetSize = 0;
        
        REI_API AssetRef(std::string id) : Id(std::move(id))
        {
        }

        AssetRef(const AssetRef& other)
            : IsLoaded(other.IsLoaded),
              Id(other.Id),
              Asset(other.Asset)
        {
        }

        T* operator->()
        {
            REI_ASSERT(Id != "", "Missing asset Id")
            REI_ASSERT(IsLoaded, "Asset id=" + Id + " is not loaded")
            
            return Asset;
        }

        bool VerifyIsLoaded() const
        {
            if (IsLoaded) return true;

            LOG_ERROR("Asset id={} is not loaded", Id)
            return false;
        }

        void UnloadAsset() override
        {
            delete Asset;
            IsLoaded = false;
        }

        i32 GetAssetSize() override
        {
            return AssetSize;
        }
    };
}
