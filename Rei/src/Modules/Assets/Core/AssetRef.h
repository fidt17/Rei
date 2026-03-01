#pragma once

#include "Modules/Assets/Registry/AssetRecord.h"

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

        virtual i32 GetAssetSize() = 0;
    };

    template <typename T>
    struct AssetRef : public IAssetRef
    {
        SERIALIZABLE_BODY(AssetRef)

        SERIALIZE std::string Id = "";
        std::shared_ptr<AssetRecord> Record = nullptr;

        using AssignHandler = void (*)(AssetRef<T>&, const AssetRef<T>&);
        inline static AssignHandler AssignHandlerFunc = nullptr;

        REI_API AssetRef(std::string id)
            :
            Id(std::move(id))
        {
        }

        REI_API AssetRef(const AssetRef& other)
            : Id(other.Id),
              Record(other.Record)
        {
        }

        REI_API AssetRef& operator=(const AssetRef& other)
        {
            if (this == &other) return *this;

            if (AssignHandlerFunc != nullptr)
            {
                AssignHandlerFunc(*this, other);
                return *this;
            }

            Id = other.Id;
            Record = other.Record;
            return *this;
        }

        REI_API T* operator->()
        {
            REI_ASSERT(Id != "", "Missing asset Id")
            REI_ASSERT(IsLoaded(), "Asset id=" + Id + ", name=" + GetName() + " is not loaded")

            return Get();
        }

        REI_API const T* operator->() const
        {
            REI_ASSERT(Id != "", "Missing asset Id")
            REI_ASSERT(IsLoaded(), "Asset id=" + Id + ", name=" + GetName() + " is not loaded")

            return Get();
        }

        REI_API bool IsLoaded() const
        {
            if (Record == nullptr) return false;

            const bool hasValue = Record->Value != nullptr;
            return Record->Id == Id && Record->State == AssetState::Loaded && hasValue;
        }

        REI_API std::string GetBoundId() const
        {
            if (Record == nullptr || Record->State != AssetState::Loaded) return "";

            return Record->Id;
        }
        
        REI_API std::string GetName() const
        {
            if (Record == nullptr) return "";

            return Record->Name;
        }

        REI_API T* Get() const
        {
            if (!IsLoaded()) return nullptr;
            return static_cast<T*>(Record->Value.get());
        }

        REI_API i32 GetAssetSize() override
        {
            if (Record == nullptr) return 0;

            return Record->AssetSize;
        }
    };

#ifdef SERIALIZABLE_BODY
#pragma pop_macro("SERIALIZABLE_BODY")
#endif
}
