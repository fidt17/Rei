#pragma once
#include "BinaryReader.h"

namespace rei::assets
{
    struct AssetId
    {
        const std::string Id;

        explicit AssetId(std::string str);
        explicit AssetId(BinaryReader& reader);
    };
}
