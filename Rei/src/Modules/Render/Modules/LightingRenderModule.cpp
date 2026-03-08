#include "pch.h"
#include "LightingRenderModule.h"

#include "rei_behaviours/transformation/Transform.h"

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
    ECS_WORLD(rei::GetInternalWorld());
    const auto ambientLights = FILTER(AmbientLight);
    GetInternalWorld()->RefreshAll();

    if (ambientLights->GetEntitiesCount() == 0) return;
    _ambientLight = GET_REF(*ambientLights->begin(), rei::render::AmbientLight);
}

void rei::render::LightingRenderModule::FindPointLights()
{
    ECS_WORLD(rei::GetInternalWorld());
    const auto pointLights = FILTER(PointLight);
    GetInternalWorld()->RefreshAll();

    _pointLights.clear();
    FOR(e, pointLights)
    {
        _pointLights.emplace_back(GET_REF(e, rei::render::PointLight));
    }
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
    for (i32 i = 0; i < _pointLights.size(); i++)
    {
        if (i > REI_MAX_POINT_LIGHTS_COUNT) break;

        const auto& light = _pointLights[i];
        if (light.IsNull()) continue;

        shader.SetVector3("_PointLights[" + std::to_string(i) + "].Position", light.Get().GetTransform().GetPosition());
        shader.SetFloat("_PointLights[" + std::to_string(i) + "].Strength", light.Get().GetStrength());
        shader.SetColor("_PointLights[" + std::to_string(i) + "].Color", light.Get().GetColor());
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
        shader.SetViewMatrices(_cameraModule->GetProjectionMatrix(), _cameraModule->GetViewMatrix(), light.Get().GetTransform().CalculateModelMatrix());

        _cubeVertexData.Render();
    }
}

