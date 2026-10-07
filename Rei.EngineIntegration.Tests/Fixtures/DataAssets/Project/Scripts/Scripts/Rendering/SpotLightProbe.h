#pragma once
#include <Core.h>
#include <rei_behaviours/render/light/SpotLight.h>
#include <rei_behaviours/render/camera/Camera.h>
#include "SpotLightProbeData.h"

namespace rei::testing
{
    class SpotLightProbe final : public Behaviour
    {
        BEHAVIOUR_BODY(SpotLightProbe)

        SERIALIZE assets::AssetRef<SpotLightProbeData> _output;
        SERIALIZE ecs::ComponentRef<render::SpotLight> _light;

    public:
        void Update() override
        {
            if (!_output.IsLoaded() || _light.IsNull()) return;
            const auto& light = _light.Get();
            _output->Strength = light.GetStrength();
            _output->Range = light.GetRange();
            _output->InnerAngle = light.GetInnerAngle();
            _output->OuterAngle = light.GetOuterAngle();
            _output->Color = light.GetColor();
            _output->Direction = light.GetWorldDirection();
            _output->Position = light.GetTransform().GetWorldPosition();
            const auto camera = render::Camera::GetMainCamera();
            const auto* profile = camera.IsNull() ? nullptr : camera.Get().GetRendererSettings().Get();
            _output->MaxSpotLights = profile ? profile->GetMaxSpotLights() : render::RendererSettings{}.GetMaxSpotLights();
            ++_output->Samples;
        }
    };
}
