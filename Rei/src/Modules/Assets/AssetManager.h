#pragma once
#include <filesystem>

#include "BinaryReader.h"
#include "AssetRef.h"

namespace rei::assets
{
    class AssetManager
    {
    public:
        explicit AssetManager(const std::string& resourcesPath);

        template <typename T>
        T Load(const std::string& path) const
        {
            SET_LOG_SCOPE("AssetManager")
            LOG_WARNING("Loading asset from: " + path)
            
            auto reader = BinaryReader(path);
            return reader.Get<T>();
        }

        template <typename T>
        T Load(const AssetRef ref) const
        {
            return Load<T>(ref.AssetId.Id + ".bin");
        }
    };
}
