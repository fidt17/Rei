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

        virtual void UnloadAsset() = 0;
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

        REI_API AssetRef(std::string id) : Id(std::move(id)) { }

        AssetRef(const AssetRef& other)
            : Id(other.Id),
              Record(other.Record) { }

        AssetRef& operator=(const AssetRef& other)
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

        T* operator->()
        {
            REI_ASSERT(Id != "", "Missing asset Id")
            REI_ASSERT(IsLoaded(), "Asset id=" + Id + " is not loaded")

            return Get();
        }

        const T* operator->() const
        {
            REI_ASSERT(Id != "", "Missing asset Id")
            REI_ASSERT(IsLoaded(), "Asset id=" + Id + " is not loaded")

            return Get();
        }

        bool IsLoaded() const
        {
            if (Record == nullptr) return false;

            const bool hasValue = Record->OwnedValue != nullptr || Record->ExternalValue != nullptr;
            return Record->Id == Id && Record->State == AssetState::Loaded && hasValue;
        }

        std::string GetBoundId() const
        {
            if (Record == nullptr || Record->State != AssetState::Loaded) return "";

            return Record->Id;
        }

        T* Get() const
        {
            if (!IsLoaded()) return nullptr;
            if (Record->OwnedValue != nullptr) return static_cast<T*>(Record->OwnedValue.get());

            return static_cast<T*>(Record->ExternalValue);
        }

        void UnloadAsset() override
        {
            if (Record == nullptr) return;
            
            Record->OwnedValue.reset();
            Record->ExternalValue = nullptr;
            Record->AssetSize = 0;
            Record->State = AssetState::Unloaded;
        }

        i32 GetAssetSize() override
        {
            if (Record == nullptr) return 0;

            return Record->AssetSize;
        }
    };

#ifdef SERIALIZABLE_BODY
    #pragma pop_macro("SERIALIZABLE_BODY")
#endif
}

