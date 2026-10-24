#pragma once

#include <memory>
#include <mutex>
#include <string>
#include <typeindex>

#include "AssetDataAccessor.h"
#include "AssetState.h"

namespace rei::assets
{
    REI_API struct AssetRecord
    {
        std::mutex LoadMutex;
        std::string Id;
        std::string Name;
        std::type_index Type = typeid(void);
        AssetState State = AssetState::Unloaded;
        i32 AssetSize = 0;
        std::shared_ptr<void> Value = nullptr;
        std::unique_ptr<IAssetDataAccessor> DataAccessor;
    };
}
