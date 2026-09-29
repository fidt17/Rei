#pragma once

#include <Core.h>

namespace rei::testing
{
    class FrameTimeProbeData
    {
        DATA_ASSET_BODY(FrameTimeProbeData)

    public:
        SERIALIZE i32 FrameCount = 0;
        SERIALIZE f32 FirstDelta = -1;
        SERIALIZE f32 Delta = 0;
        SERIALIZE f32 Elapsed = 0;
        SERIALIZE f32 SessionProgress = 0;
        SERIALIZE i32 InvalidFrames = 0;
        SERIALIZE i32 UnstableReads = 0;
    };
}
