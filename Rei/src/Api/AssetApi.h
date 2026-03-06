#pragma once

#include <concepts>

#include "Engine/Services.h"
#include "Modules/Render/Material/Material.h"

template <typename T>
concept SerializableAsset = requires(T t, const T ct, const nlohmann::json& data)
{
    { ct.REI_GET() } -> std::same_as<nlohmann::json>;
    { t.REI_SET(data) } -> std::same_as<void>;
};

template <typename T>
requires SerializableAsset<T>
inline bool TryGetAssetDataImpl(const std::string& assetId, char* outputBuffer, const int bufferSize)
{
    auto asset = rei::GetAssetManager().GetById<T>(assetId);
    if (!asset.IsLoaded()) return false;

    const auto data = asset->REI_GET();
    strncpy_s(outputBuffer, bufferSize, data.dump().c_str(), _TRUNCATE);
    return true;
}

template <typename T>
requires SerializableAsset<T>
inline bool TrySetAssetDataImpl(const std::string& assetId, const std::string& jsonData)
{
    auto asset = rei::GetAssetManager().GetById<T>(assetId);
    if (!asset.IsLoaded()) return false;

    const auto data = nlohmann::json::parse(jsonData);
    asset->REI_SET(data);
    return true;
}

inline bool DispatchTryGetAssetData(const std::string& assetId, const std::string& assetType, char* outputBuffer, const int bufferSize)
{
    if (assetType == "Material")
    {
        return TryGetAssetDataImpl<rei::render::Material>(assetId, outputBuffer, bufferSize);
    }

    return false;
}

inline bool DispatchTrySetAssetData(const std::string& assetId, const std::string& assetType, const std::string& jsonData)
{
    if (assetType == "Material")
    {
        return TrySetAssetDataImpl<rei::render::Material>(assetId, jsonData);
    }

    return false;
}

REI_EXTERN_API inline bool GetAssetData(const char* assetId, const char* assetType, char* outputBuffer, const int bufferSize)
{
    if (assetId == nullptr || assetType == nullptr || outputBuffer == nullptr || bufferSize <= 0) return false;

    bool success = false;
    const std::string assetIdStr = assetId;
    const std::string assetTypeStr = assetType;

    rei::GetEngine().ExecuteOnMainThread([&]
    {
        try
        {
            success = DispatchTryGetAssetData(assetIdStr, assetTypeStr, outputBuffer, bufferSize);
        }
        catch (const std::exception& e)
        {
            LOG_ERROR("GetAssetData failed for assetId='{}'. Error: {}", assetIdStr, e.what())
            success = false;
        }
        catch (...)
        {
            LOG_ERROR("GetAssetData failed for assetId='{}'", assetIdStr)
            success = false;
        }
    })->WaitForCompletion();

    return success;
}

REI_EXTERN_API inline bool SetAssetData(const char* assetId, const char* assetType, const char* json)
{
    if (assetId == nullptr || assetType == nullptr || json == nullptr) return false;

    bool success = false;
    const std::string assetIdStr = assetId;
    const std::string assetTypeStr = assetType;
    const std::string jsonStr = json;

    rei::GetEngine().ExecuteOnMainThread([&]
    {
        try
        {
            success = DispatchTrySetAssetData(assetIdStr, assetTypeStr, jsonStr);
        }
        catch (const std::exception& e)
        {
            LOG_ERROR("SetAssetData failed for assetId='{}'. Error: {}", assetIdStr, e.what())
            success = false;
        }
        catch (...)
        {
            LOG_ERROR("SetAssetData failed for assetId='{}'", assetIdStr)
            success = false;
        }
    })->WaitForCompletion();

    return success;
}

REI_EXTERN_API inline bool PatchAssetData(const char* assetId, const char* assetType, const char* jsonPatch)
{
    return SetAssetData(assetId, assetType, jsonPatch);
}
