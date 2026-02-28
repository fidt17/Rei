#pragma once
#include "Scene.h"
#include "Modules/Assets/Core/AssetRef.h"

namespace rei::scenes
{
    struct BuildScenesConfig
    {
        explicit BuildScenesConfig(resources::BinaryReader& reader);

        bool Has(u32 id) const;
        assets::AssetRef<Scene>& GetScene(u32 id);

    private:
        std::unordered_map<u32, assets::AssetRef<Scene>> _buildScenes;
    };
}
