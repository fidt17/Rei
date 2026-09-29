#pragma once

#include <concepts>
#include <memory>

#include "../../../../external/json.hpp"

namespace rei::assets
{
    class IAssetDataAccessor
    {
    public:
        virtual ~IAssetDataAccessor() = default;

        virtual bool TryGetData(nlohmann::json& data) const = 0;
        virtual bool TrySetData(const nlohmann::json& data) = 0;
    };

    template <typename T>
    class AssetDataAccessor final : public IAssetDataAccessor
    {
    public:
        static constexpr bool IS_SUPPORTED = requires(T& asset, const T& constAsset, const nlohmann::json& data)
        {
            { constAsset.REI_GET() } -> std::same_as<nlohmann::json>;
            { asset.REI_SET(data) } -> std::same_as<void>;
        };

        explicit AssetDataAccessor(const std::shared_ptr<T>& asset) : _asset(asset)
        {
        }

        bool TryGetData(nlohmann::json& data) const override
        {
            const auto asset = _asset.lock();
            if (asset == nullptr) return false;

            if constexpr (IS_SUPPORTED)
            {
                data = asset->REI_GET();
                return true;
            }

            return false;
        }

        bool TrySetData(const nlohmann::json& data) override
        {
            const auto asset = _asset.lock();
            if (asset == nullptr) return false;

            if constexpr (IS_SUPPORTED)
            {
                asset->REI_SET(data);
                if constexpr (requires(T& value) { value.ResolveDependencies(); }) asset->ResolveDependencies();
                return true;
            }

            return false;
        }

    private:
        std::weak_ptr<T> _asset;
    };
}
