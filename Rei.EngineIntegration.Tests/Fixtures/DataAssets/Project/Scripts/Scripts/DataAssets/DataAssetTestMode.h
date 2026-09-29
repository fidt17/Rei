#pragma once

#include <Core.h>

namespace symbols::testing
{
    SERIALIZABLE_ENUM(DataAssetTestMode)
    {
        Primary,
        Secondary,
        Disabled
    };
}
