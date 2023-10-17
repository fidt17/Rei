#include "pch.h"
#include "BuildScenesConfig.h"

#include "Modules/Assets/AssetId.h"
#include "Modules/Assets/AssetRef.h"

namespace rei::scenes
{
    BuildScenesConfig::BuildScenesConfig(assets::BinaryReader& reader)
    {
        const auto scenesCount = reader.GetI32();
        for (auto i = 0; i < scenesCount; i++)
        {
            auto sceneId = reader.GetU32();
            const auto assetId = reader.Get<assets::AssetId>();

            _buildScenes.insert({sceneId, assets::AssetRef(assetId)});
        }
    }

    bool BuildScenesConfig::Has(const u32 id) const
    {
        return _buildScenes.count(id);
    }

    assets::AssetRef& BuildScenesConfig::GetScene(const u32 id)
    {
        return _buildScenes[id];
    }
}
