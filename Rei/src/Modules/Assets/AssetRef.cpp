#include "pch.h"
#include "AssetRef.h"

namespace rei::assets
{
    AssetRef::AssetRef(): AssetId("")
    {
    }

    AssetRef::AssetRef(assets::AssetId assetId): AssetId(std::move(assetId))
    {
    }
}
