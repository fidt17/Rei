#pragma once

#include "Engine/Services.h"
#include "Modules/Render/Material/Material.h"

// Project-defined asset types are only known to their DLL. Both built-in and
// project asset JSON access goes through the typed adapter stored on AssetRecord.
template <typename Action>
inline bool WithSerializableAssetData(const std::string& assetId, const std::string& assetType, Action&& action)
{
    if (assetType != "Material" && assetType != "DataAsset") return false;
    if (action()) return true;
    if (assetType != "Material") return false;

    // Preserve lazy loading for built-in materials. Project DataAssets must
    // already be loaded through an AssetRef<T> in the project DLL.
    const auto material = rei::GetAssetManager().GetById<rei::render::Material>(assetId);
    return material.IsLoaded() && action();
}

inline bool DispatchTryGetAssetData(const std::string& assetId, const std::string& assetType, char* outputBuffer, const i32 bufferSize)
{
    nlohmann::json data;
    if (!WithSerializableAssetData(assetId, assetType, [&]
    {
        return rei::GetAssetManager().TryGetLoadedAssetData(assetId, data);
    })) return false;

    const auto json = data.dump();
    if (json.size() >= static_cast<u64>(bufferSize))
    {
        outputBuffer[0] = '\0';
        return false;
    }

    strcpy_s(outputBuffer, bufferSize, json.c_str());
    return true;
}

inline bool DispatchTrySetAssetData(const std::string& assetId, const std::string& assetType, const std::string& jsonData)
{
    const auto data = nlohmann::json::parse(jsonData);
    return WithSerializableAssetData(assetId, assetType, [&]
    {
        return rei::GetAssetManager().TrySetLoadedAssetData(assetId, data);
    });
}

REI_EXTERN_API inline bool GetAssetData(const char* assetId, const char* assetType, char* outputBuffer, const i32 bufferSize)
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
