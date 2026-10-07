#pragma once
#include <Core.h>
#include <rei_behaviours/render/camera/Camera.h>
#include "HdrProbeData.h"

namespace rei::testing
{
    class HdrProbe final : public Behaviour
    {
        BEHAVIOUR_BODY(HdrProbe)

        SERIALIZE assets::AssetRef<HdrProbeData> _output;
        SERIALIZE ecs::ComponentRef<render::Camera> _camera;

    public:
        void Update() override
        {
            const auto camera = _camera.IsNull() ? render::Camera::GetMainCamera() : _camera;
            if (!_output.IsLoaded() || camera.IsNull()) return;
            const auto& ref = camera.Get().GetRendererSettings();
            const auto* settings = ref.Get();
            _output->ProfileId = ref.Id;
            _output->ExposureEV = settings ? settings->GetExposure() : 0;
            _output->ToneMapping = settings ? static_cast<i32>(settings->GetToneMapping()) : 0;
            _output->MaxPointLights = settings ? settings->GetMaxPointLights() : render::RendererSettings{}.GetMaxPointLights();
            ++_output->Samples;
        }
    };
}
