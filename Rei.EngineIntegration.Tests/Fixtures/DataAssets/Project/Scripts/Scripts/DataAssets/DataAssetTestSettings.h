#pragma once

#include <string>

#include <Core.h>

namespace symbols::testing
{
    struct DataAssetTestSettings
    {
        SERIALIZABLE_BODY(DataAssetTestSettings)
        SERIALIZE f32 Value = 1.25f;
        SERIALIZE std::string Label = "Before";
    };
}
