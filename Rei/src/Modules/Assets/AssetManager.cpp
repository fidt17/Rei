#include "pch.h"
#include "AssetManager.h"

namespace rei::assets
{
    AssetManager::AssetManager(const std::string& resourcesPath)
    {
        current_path(std::filesystem::path(resourcesPath));
    }
}
