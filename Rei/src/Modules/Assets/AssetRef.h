#pragma once
#include "AssetId.h"

namespace rei::assets
{
    struct AssetRef
    {
        const AssetId AssetId;

        REI_API explicit AssetRef();
        REI_API explicit AssetRef(assets::AssetId assetId);
    };
}
