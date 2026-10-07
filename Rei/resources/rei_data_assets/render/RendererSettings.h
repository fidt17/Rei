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

        REI_RANGE(-16.0f, 16.0f)
        SERIALIZE f32 _exposure = 0;
        SERIALIZE ToneMappingMode _toneMapping = Reinhard;
        REI_RANGE(0, 8)
        SERIALIZE i32 _maxPointLights = 4;

    public:
        f32 GetExposure() const { return std::isfinite(_exposure) ? std::clamp(_exposure, -16.0f, 16.0f) : 0.0f; }
        f32 GetExposureEV() const { return GetExposure(); }
        ToneMappingMode GetToneMapping() const { return _toneMapping == Reinhard ? Reinhard : Off; }
        i32 GetMaxPointLights() const { return std::clamp(_maxPointLights, 0, REI_MAX_POINT_LIGHTS_COUNT); }

        void SetExposure(f32 exposure) { _exposure = exposure; }
        void SetExposureEV(f32 exposureEV) { SetExposure(exposureEV); }
        void SetToneMapping(ToneMappingMode toneMapping) { _toneMapping = toneMapping; }
        void SetMaxPointLights(i32 maxPointLights) { _maxPointLights = maxPointLights; }
    };
}
