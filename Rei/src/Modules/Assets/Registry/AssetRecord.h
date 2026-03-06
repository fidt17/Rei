#pragma once

#include <memory>
#include <string>
#include <typeindex>

#include "AssetState.h"

namespace rei::assets
{
    REI_API struct AssetRecord
    {
        std::string Id;
        std::string Name;
        std::type_index Type = typeid(void);
        AssetState State = AssetState::Unloaded;
        i32 AssetSize = 0;
        std::shared_ptr<void> Value = nullptr;
    };
}
