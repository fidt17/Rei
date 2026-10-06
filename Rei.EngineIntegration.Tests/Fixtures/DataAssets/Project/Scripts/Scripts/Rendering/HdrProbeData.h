#pragma once
#include <Core.h>

namespace rei::testing
{
    class HdrProbeData
    {
        DATA_ASSET_BODY(HdrProbeData)

    public:
        SERIALIZE std::string ProfileId;
        SERIALIZE f32 ExposureEV = 0;
        SERIALIZE i32 ToneMapping = 0;
        SERIALIZE i32 MaxPointLights = 8;
        SERIALIZE i32 Samples = 0;
    };
}
