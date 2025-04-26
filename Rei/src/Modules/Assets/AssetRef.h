#pragma once

namespace rei::assets
{
    typedef std::string AssetId;

    class IAssetRef
    {
    public:
        virtual ~IAssetRef() = default;
    };
    
    template <typename T>
    struct AssetRef : public IAssetRef
    {
        bool IsLoaded = false;
        AssetId Id;
        T* Asset;

        REI_API AssetRef(AssetId id = "") : Id(std::move(id))
        {
        }

        AssetRef(const AssetRef& other)
            : IsLoaded(other.IsLoaded),
              Id(other.Id),
              Asset(other.Asset)
        {
        }

        AssetRef& operator=(const AssetRef& other)
        {
            if (this == &other)
                return *this;
            IsLoaded = other.IsLoaded;
            Id = other.Id;
            Asset = other.Asset;
            return *this;
        }

        T* operator->()
        {
            REI_ASSERT(IsLoaded, "Asset" + Id + " is not loaded")
            
            return Asset;
        }
    };
}
