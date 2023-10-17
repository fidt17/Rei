#include "pch.h"
#include "AssetsMap.h"

rei::assets::AssetsMap::AssetsMap(BinaryReader& reader)
{
    const i32 count = reader.GetI32();

    for (auto i = 0; i < count; i++)
    {
        const auto id = reader.GetStr();
        const auto path = reader.GetStr();
        const i64 offset = reader.GetI64();

        _assets.insert({id, BuildAssetInfo(path, offset)});
    }
}

rei::assets::BuildAssetInfo rei::assets::AssetsMap::GetAssetInfo(const std::string& id) const
{
    REI_ASSERT(_assets.count(id) != 0, "Missing asset with id: " + id)

    return _assets.at(id);
}
