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

        BuildAssetInfo(std::string path, const i64 offset)
            : Path(std::move(path)),
              Offset(offset)
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
