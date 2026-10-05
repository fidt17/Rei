#include "pch.h"
#include "LightingRenderModule.h"

#include "Modules/Components/ActiveTag.h"
#include "rei_behaviours/transformation/Transform.h"

namespace
{
    template <typename TLight, typename TVisitor>
    void VisitEnabledLights(TVisitor visitor)
    {
        ECS_WORLD(rei::GetInternalWorld());
        const auto lights = FILTER(TLight, rei::ActiveTag);
        FOR(entity, lights)
        {
            const auto light = GET_REF(entity, TLight);
            if (!light.Get().IsEnabled()) continue;
            if (!visitor(light)) break;
        }
    }
}

rei::render::LightingRenderModule::LightingRenderModule(const std::shared_ptr<CameraModule>& cameraModule): _cameraModule(cameraModule)
{
}

void rei::render::LightingRenderModule::Setup()
{
    _lightSourceMaterial = GetAssetManager().GetById<Material>(REI_LIGHT_SOURCE_MATERIAL_ID);
}

void rei::render::LightingRenderModule::OnBeforeRender()
{
    FindAmbientLights();
    FindPointLights();
}

void rei::render::LightingRenderModule::Render() const
{
    RenderPointLights();
}

void rei::render::LightingRenderModule::SetLightValues(const Shader& shader) const
{
    SetAmbientLight(shader);
    SetPointLights(shader);
}

void rei::render::LightingRenderModule::FindAmbientLights()
{
    _ambientLight = {};
    VisitEnabledLights<AmbientLight>([this](const auto& light)
    {
        _ambientLight = light;
        return false;
    });
}

void rei::render::LightingRenderModule::FindPointLights()
{
    _pointLights.clear();
    VisitEnabledLights<PointLight>([this](const auto& light)
    {
        _pointLights.emplace_back(light);
        return true;
    });
}

void rei::render::LightingRenderModule::SetAmbientLight(const Shader& shader) const
{
    if (_ambientLight.IsNull())
    {
        shader.SetFloat("_AmbientLight.Strength", 0);
        shader.SetColor("_AmbientLight.Color", Color(0, 0, 0, 1));
        return;
    }

    shader.SetFloat("_AmbientLight.Strength", _ambientLight.Get().GetStrength());

    const auto& c = _ambientLight.Get().GetColor();
    shader.SetColor("_AmbientLight.Color", c);
}

void rei::render::LightingRenderModule::SetPointLights(const Shader& shader) const
{
    // TODO: Select lights affecting each rendered object; currently the point-light cap applies to the entire scene.
    i32 count = 0;
    for (const auto& light : _pointLights)
    {
        if (count == REI_MAX_POINT_LIGHTS_COUNT) break;
        if (light.IsNull()) continue;

        const auto slot = "_PointLights[" + std::to_string(count) + "]";
        shader.SetVector3(slot + ".Position", light.Get().GetTransform().GetWorldPosition());
        shader.SetFloat(slot + ".Strength", light.Get().GetStrength());
        shader.SetFloat(slot + ".Range", light.Get().GetRange());
        shader.SetColor(slot + ".Color", light.Get().GetColor());
        ++count;
    }
    shader.SetInt("_PointLightsCount", count);

    for (i32 i = count; i < REI_MAX_POINT_LIGHTS_COUNT; ++i)
    {
        const auto slot = "_PointLights[" + std::to_string(i) + "]";
        shader.SetVector3(slot + ".Position", {0, 0, 0});
        shader.SetFloat(slot + ".Strength", 0);
        shader.SetFloat(slot + ".Range", 0);
        shader.SetColor(slot + ".Color", Color(0, 0, 0, 1));
    }
}

void rei::render::LightingRenderModule::RenderPointLights() const
{
    for (auto& light : _pointLights)
    {
        if (light.IsNull()) return;

        const auto& shader = _lightSourceMaterial->GetShader();
        shader.SetColor("_Color", light.Get().GetColor());
        shader.SetFloat("_Strength", light.Get().GetStrength());
        shader.SetViewMatrices(_cameraModule->GetProjectionMatrix(), _cameraModule->GetViewMatrix(), light.Get().GetTransform().CalculateWorldModelMatrix());

        _cubeVertexData.Render();
    }
}
