#pragma once
#include <Core.h>

namespace rei::testing
{
    class SpotLightProbeData
    {
        DATA_ASSET_BODY(SpotLightProbeData)

    public:
        SERIALIZE f32 Strength = 0;
        SERIALIZE f32 Range = 0;
        SERIALIZE f32 InnerAngle = 0;
        SERIALIZE f32 OuterAngle = 0;
        SERIALIZE render::Color Color;
        SERIALIZE math::Vector3 Direction;
        SERIALIZE math::Vector3 Position;
        SERIALIZE i32 MaxSpotLights = 0;
        SERIALIZE i32 Samples = 0;
    };
}
