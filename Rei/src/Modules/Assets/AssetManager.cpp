#include "pch.h"
#include "AssetManager.h"

#include "Engine/Services.h"

namespace rei::assets
{
    AssetManager::AssetManager(const std::string& resourcesPath)
    {
        Services::GetInstance()->SetAssetManager(this);
        
        current_path(std::filesystem::path(resourcesPath));

        _map = std::make_unique<AssetsMap>(Load<AssetsMap>("map.bin", 0));
    }
}
