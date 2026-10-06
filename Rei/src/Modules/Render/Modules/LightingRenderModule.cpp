#include "pch.h"
#include "LightingRenderModule.h"
#include "Common/Profiling/ProfileMarkers.h"

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
    BuildSnapshot();
    _lightLocations.clear();
}

void rei::render::LightingRenderModule::Render() const
{
    RenderPointLights();
}

void rei::render::LightingRenderModule::SetLightValues(const Shader& shader) const
{
    REI_PROFILE_SCOPE(profiling::markers::LIGHTING_APPLY.Id);
    profiling::UniformPhaseScope phase(profiling::UniformPhase::Lighting);
    const auto& locations = GetLightLocations(shader);
    Shader::UniformBatch uniforms(shader, false);
    shader.SetFloat(locations[0], _ambientStrength);
    shader.SetLinearColor(locations[1], _ambientLinearColor);
    for (i32 i = 0; i < REI_MAX_POINT_LIGHTS_COUNT; ++i)
    {
        const auto& light = _pointSnapshot[i];
        const auto slot = 2 + i * 4;
        shader.SetVector3(locations[slot], light.Position);
        shader.SetFloat(locations[slot + 1], light.Strength);
        shader.SetFloat(locations[slot + 2], light.Range);
        shader.SetLinearColor(locations[slot + 3], light.LinearColor);
    }
    shader.SetInt(locations.back(), _pointCount);
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

void rei::render::LightingRenderModule::BuildSnapshot()
{
    _ambientStrength = 0;
    _ambientLinearColor = Color(0, 0, 0, 1);
    if (!_ambientLight.IsNull())
    {
        const auto& ambient = _ambientLight.Get();
        _ambientStrength = ambient.GetStrength();
        _ambientLinearColor = ambient.GetColor().ToLinear();
    }
    _pointCount = 0;
    _pointSnapshot.fill(PointLightSnapshot{});
    // Preserve the current first-four scene order; object-specific selection is a separate stage.
    for (const auto& reference : _pointLights)
    {
        if (_pointCount == REI_MAX_POINT_LIGHTS_COUNT) break;
        if (reference.IsNull()) continue;
        const auto& light = reference.Get();
        _pointSnapshot[_pointCount++] = {light.GetTransform().GetWorldPosition(), light.GetStrength(), light.GetRange(), light.GetColor().ToLinear()};
    }
}

const rei::render::LightingRenderModule::LightLocations& rei::render::LightingRenderModule::GetLightLocations(const Shader& shader) const
{
    const auto revision = shader.GetProgramRevision();
    const auto previous = _lightLocations.find(revision);
    if (previous != _lightLocations.end()) return previous->second;
    static const auto names = []
    {
        std::array<std::string, LIGHT_UNIFORM_COUNT> values;
        values[0] = "_AmbientLight.Strength";
        values[1] = "_AmbientLight.Color";
        for (i32 i = 0; i < REI_MAX_POINT_LIGHTS_COUNT; ++i)
        {
            const auto prefix = "_PointLights[" + std::to_string(i) + "]";
            const auto slot = 2 + i * 4;
            values[slot] = prefix + ".Position";
            values[slot + 1] = prefix + ".Strength";
            values[slot + 2] = prefix + ".Range";
            values[slot + 3] = prefix + ".Color";
        }
        values.back() = "_PointLightsCount";
        return values;
    }();
    LightLocations locations;
    for (u32 i = 0; i < LIGHT_UNIFORM_COUNT; ++i) locations[i] = shader.GetLocation(names[i]);
    return _lightLocations.emplace(revision, locations).first->second;
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
