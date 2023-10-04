#include "pch.h"
#include "AssetId.h"

namespace rei::assets
{
    AssetId::AssetId(std::string str): Id(std::move(str))
    { }

    AssetId::AssetId(BinaryReader& reader)
        : Id(reader.GetStr())
    {
    }
}
