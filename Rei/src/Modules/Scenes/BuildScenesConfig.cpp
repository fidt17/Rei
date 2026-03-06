#include "pch.h"
#include "BuildScenesConfig.h"

#include "Modules/Assets/Core/AssetRef.h"
#include "../external/json.hpp"

using json = nlohmann::json;

namespace rei::scenes
{
    BuildScenesConfig::BuildScenesConfig(resources::BinaryReader& reader)
    {
        const auto str = reader.GetStr();
        json data = json::parse(str);

        auto m = data.at("Scenes").get<std::map<std::string, std::string>>();

        for (const auto& [key, val] : m)
        {
            auto idx = std::stoi(key);
            _buildScenes.insert({idx, assets::AssetRef<Scene>(val)});
        }
    }

    bool BuildScenesConfig::Has(const u32 id) const
    {
        return _buildScenes.count(id);
    }

    assets::AssetRef<Scene>& BuildScenesConfig::GetScene(const u32 id)
    {
        return _buildScenes[id];
    }
}
