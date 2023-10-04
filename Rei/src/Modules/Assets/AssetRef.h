#pragma once
#include "AssetId.h"

namespace rei::assets
{
    struct AssetRef
    {
        const AssetId AssetId;

        explicit AssetRef();
        explicit AssetRef(assets::AssetId assetId);
    };
}
