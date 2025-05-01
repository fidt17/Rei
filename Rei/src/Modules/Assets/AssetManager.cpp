#include "pch.h"
#include "AssetManager.h"

#include "Engine/Services.h"

namespace rei::assets
{
    AssetManager::AssetManager()
    {
        const std::string currentPath = std::filesystem::current_path().string();

        std::vector<std::string> checkPaths;
        checkPaths.push_back(currentPath);
        checkPaths.push_back(currentPath + "/Resources");
        checkPaths.push_back(currentPath + "/../Resources");
        checkPaths.push_back("C:/Repos/Rei Projects/New Project/New Project/bin/Resources"); // path for sandbox testing

        bool didFindResources = false;
        for (auto& checkPath : checkPaths)
        {
            std::cout << ("[AssetManager] Check resources path: " + checkPath) << std::endl;
            if (std::filesystem::exists(checkPath + "/map.bin"))
            {
                std::filesystem::current_path(checkPath);
                std::cout << ("[AssetManager] Resources path found: " + checkPath) << std::endl;
                didFindResources = true;
                break;
            }
        }

        REI_THROW_IF(!didFindResources, "[AssetManager] Resources folder is missing");
        
        _map = std::make_unique<AssetsMap>(Load<AssetsMap>("map.bin", 0));
    }
}
