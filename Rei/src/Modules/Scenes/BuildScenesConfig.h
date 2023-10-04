#pragma once
#include "Modules/Assets/AssetRef.h"
#include "Modules/Assets/BinaryReader.h"

namespace rei::scenes
{
    struct BuildScenesConfig
    {
        explicit BuildScenesConfig(assets::BinaryReader& reader);

        bool Has(u32 id) const;
        assets::AssetRef& GetScene(u32 id);

    private:
        std::unordered_map<u32, assets::AssetRef> _buildScenes;
    };
}
