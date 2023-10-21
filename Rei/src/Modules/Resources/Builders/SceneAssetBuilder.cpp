#include "pch.h"
#include "SceneAssetBuilder.h"

void rei::resources::BuildSceneAsset(const std::filesystem::path& assetPath, BinaryWriter& writer)
{
    const auto json = nlohmann::json::parse(std::ifstream(assetPath.string()));
    writer.WriteStr(json.at("Name").get<std::string>());
}
