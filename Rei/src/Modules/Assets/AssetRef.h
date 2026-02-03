#pragma once

namespace rei::assets
{
#ifdef SERIALIZABLE_BODY
    #pragma push_macro("SERIALIZABLE_BODY")
    #undef SERIALIZABLE_BODY
    #define SERIALIZABLE_BODY(CLASS_NAME)\
        public:\
        CLASS_NAME() = default;\
        nlohmann::json REI_GET() const;\
        void REI_SET(const nlohmann::json& data);
#endif

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

        SERIALIZE std::string Id = "";
        std::string LoadedId = "";

        T* Asset = nullptr;
        i32 AssetSize = 0;
        
        using AssignHandler = void (*)(AssetRef<T>&, const AssetRef<T>&);
        inline static AssignHandler AssignHandlerFunc = nullptr;

        REI_API AssetRef(std::string id) : Id(std::move(id))
        {
        }

        AssetRef(const AssetRef& other)
            : Id(other.Id),
              LoadedId(other.LoadedId),
              Asset(other.Asset),
              AssetSize(other.AssetSize)
        {
        }
        
        AssetRef& operator=(const AssetRef& other)
        {
            if (this == &other)
            {
                return *this;
            }

            if (AssignHandlerFunc != nullptr)
            {
                AssignHandlerFunc(*this, other);
                return *this;
            }
            
            Id = other.Id;
            LoadedId = other.LoadedId;
            Asset = other.Asset;
            AssetSize = other.AssetSize;
            return *this;
        }

        T* operator->()
        {
            REI_ASSERT(Id != "", "Missing asset Id")
            REI_ASSERT(IsLoaded(), "Asset id=" + Id + " is not loaded")

            return Asset;
        }

        bool IsLoaded() const
        {
            return Asset && LoadedId == Id;
        }

        void UnloadAsset() override
        {
            delete Asset;
        }

        i32 GetAssetSize() override
        {
            return AssetSize;
        }
    };

#ifdef SERIALIZABLE_BODY
    #pragma pop_macro("SERIALIZABLE_BODY")
#endif
}
