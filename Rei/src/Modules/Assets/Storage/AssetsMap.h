#pragma once

namespace rei::resources
{
    class BinaryReader;
}

namespace rei::assets
{
    struct BuildAssetInfo
    {
        const std::string Path;
        const i64 Offset;
        const std::string AssetName;

        BuildAssetInfo(std::string path, const i64 offset, std::string assetName)
            : Path(std::move(path)),
              Offset(offset),
              AssetName(std::move(assetName))
        {
        }
    };

    class AssetsMap
    {
    public:
        REI_API void Initialize();
        REI_API BuildAssetInfo GetAssetInfo(const std::string& id) const;

    private:
        bool _initialized = false;
        std::unordered_map<std::string, BuildAssetInfo> _assets;
    };
}
