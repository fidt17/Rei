#pragma once

namespace rei::assets
{
    struct BuildAssetInfo
    {
        const std::string Path;
        const i64 Offset;

        BuildAssetInfo(std::string path, const i64 offset)
            : Path(std::move(path)),
              Offset(offset)
        {
        }
    };

    class AssetsMap
    {
    public:
        explicit AssetsMap(BinaryReader& reader);

        BuildAssetInfo GetAssetInfo(const std::string& id) const;

    private:
        std::unordered_map<std::string, BuildAssetInfo> _assets;
    };
}
