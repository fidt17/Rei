#pragma once

#include <Core.h>
#include <Engine/Services.h>
#include <Modules/Assets/Core/AssetRef.h>
#include "FrameTimeProbeData.h"

namespace rei::testing
{
    class FrameTimeProbe final : public rei::Behaviour
    {
        BEHAVIOUR_BODY(FrameTimeProbe)

        SERIALIZE rei::assets::AssetRef<FrameTimeProbeData> _data;
        f64 _previousElapsed = 0;
        f64 _firstElapsed = 0;

    public:
        void Start() override
        {
            if (!_data.IsLoaded()) return;
            _data->FirstDelta = static_cast<f32>(GetTime().GetDeltaSeconds());
            _firstElapsed = _previousElapsed = GetTime().GetElapsedSeconds();
        }

        void Update() override
        {
            if (!_data.IsLoaded()) return;
            const auto delta = GetTime().GetDeltaSeconds();
            const auto elapsed = GetTime().GetElapsedSeconds();
            if (delta != GetTime().GetDeltaSeconds() || elapsed != GetTime().GetElapsedSeconds())
                _data->UnstableReads++;
            if (!std::isfinite(delta) || !std::isfinite(elapsed) || delta < 0 || elapsed < _previousElapsed ||
                (_data->FrameCount > 0 && std::abs(elapsed - _previousElapsed - delta) > 1e-9))
                _data->InvalidFrames++;
            _data->FrameCount++;
            _data->Delta = static_cast<f32>(delta);
            _data->Elapsed = static_cast<f32>(elapsed);
            _data->SessionProgress = static_cast<f32>(elapsed - _firstElapsed);
            _previousElapsed = elapsed;
        }
    };
}
