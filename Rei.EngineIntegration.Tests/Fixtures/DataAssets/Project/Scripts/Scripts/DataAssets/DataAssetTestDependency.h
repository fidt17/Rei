#pragma once

#include <Core.h>

namespace symbols::testing
{
    class DataAssetTestDependency
    {
        DATA_ASSET_BODY(DataAssetTestDependency)

        SERIALIZE f32 _value;

    public:
        f32 GetValue() const
        {
            return _value;
        }
    };
}
