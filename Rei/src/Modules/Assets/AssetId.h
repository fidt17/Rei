#pragma once
#include "BinaryReader.h"

namespace rei::assets
{
    struct AssetId
    {
        const std::string Id;

        REI_API explicit AssetId(std::string str);
        REI_API explicit AssetId(BinaryReader& reader);
    };
}
