#pragma once

namespace rei::assets
{
    enum class AssetState
    {
        Unloaded = 0,
        Loading,
        Loaded,
        Failed,
        PendingDestroy
    };
}
