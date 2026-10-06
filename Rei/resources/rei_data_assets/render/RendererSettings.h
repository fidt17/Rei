#pragma once
#include "Modules/Render/ToneMappingMode.h"
#include <algorithm>
#include <cmath>

namespace rei::render
{
    // Shared camera rendering profile. Serialization and dependency loading use the project registry.
    class RendererSettings
    {
        DATA_ASSET_BODY(RendererSettings)

        SERIALIZE f32 _exposureEV = 0;
        SERIALIZE ToneMappingMode _toneMapping = Reinhard;
        SERIALIZE i32 _maxPointLights = 8;

    public:
        f32 GetExposureEV() const { return std::isfinite(_exposureEV) ? std::clamp(_exposureEV, -16.0f, 16.0f) : 0.0f; }
        ToneMappingMode GetToneMapping() const { return _toneMapping == Reinhard ? Reinhard : Off; }
        i32 GetMaxPointLights() const { return std::clamp(_maxPointLights, 0, REI_MAX_POINT_LIGHTS_COUNT); }

        void SetExposureEV(f32 exposureEV) { _exposureEV = exposureEV; }
        void SetToneMapping(ToneMappingMode toneMapping) { _toneMapping = toneMapping; }
        void SetMaxPointLights(i32 maxPointLights) { _maxPointLights = maxPointLights; }
    };
}
