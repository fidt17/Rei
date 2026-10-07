#include "pch.h"
#include "LightingRenderModule.h"
#include "Common/Profiling/ProfileMarkers.h"


rei::render::LightingRenderModule::LightingRenderModule(const std::shared_ptr<CameraModule>& cameraModule): _cameraModule(cameraModule)
{
}

void rei::render::LightingRenderModule::OnBeforeRender()
{
    _selector.BeginFrame();
    _snapshot.Update(_cameraModule->GetCamera());
    _lightLocations.clear();
}

void rei::render::LightingRenderModule::SetLightValues(const Shader& shader, const math::Bounds& localBounds, const glm::mat4& modelMatrix, const ecs::Entity object) const
{
    REI_PROFILE_SCOPE(profiling::markers::LIGHTING_APPLY.Id);
    profiling::UniformPhaseScope phase(profiling::UniformPhase::Lighting);
    const auto& locations = GetLightLocations(shader);
    Shader::UniformBatch uniforms(shader, false);
    shader.SetFloat(locations[0], _snapshot.GetAmbientStrength());
    shader.SetLinearColor(locations[1], _snapshot.GetAmbientColor());
    // Fixed-count/custom shaders can use light slots without _PointLightsCount.
    if (std::none_of(locations.begin() + 2, locations.end(), [](const i32 location) { return location >= 0; })) return;
    const auto& selected = _selector.Select(localBounds, modelMatrix, object, _snapshot);
    profiling::Count(profiling::markers::LIGHTING_OBJECTS.Id);
    profiling::Count(profiling::markers::LIGHTING_SELECTED.Id, selected.Count);
    const LightSnapshot::PointLightData empty{};
    for (i32 i = 0; i < REI_MAX_POINT_LIGHTS_COUNT; ++i)
    {
        const auto& light = i < selected.Count ? _snapshot.GetPointLights()[selected.Indices[i]] : empty;
        const auto slot = 2 + i * 4;
        shader.SetVector3(locations[slot], light.Position);
        shader.SetFloat(locations[slot + 1], light.Strength);
        shader.SetFloat(locations[slot + 2], light.Range);
        shader.SetLinearColor(locations[slot + 3], light.LinearColor);
    }
    shader.SetInt(locations[POINT_COUNT_SLOT], selected.Count);
    if (std::none_of(locations.begin() + SPOT_START_SLOT, locations.end(), [](const i32 location) { return location >= 0; })) return;
    const auto& spots = _selector.SelectSpots(localBounds, modelMatrix, object, _snapshot);
    if (spots.Count == 0 && locations.back() >= 0)
    {
        shader.SetInt(locations.back(), 0);
        return;
    }
    const LightSnapshot::SpotLightData emptySpot{};
    const auto& view = _cameraModule->GetViewMatrix();
    profiling::Count(profiling::markers::LIGHTING_SELECTED.Id, spots.Count);
    for (i32 i = 0; i < REI_MAX_SPOT_LIGHTS_COUNT; ++i)
    {
        const auto& light = i < spots.Count ? _snapshot.GetSpotLights()[spots.Indices[i]] : emptySpot;
        const auto slot = SPOT_START_SLOT + i * 7;
        shader.SetVector3(locations[slot], i < spots.Count ? glm::vec3(view * glm::vec4(static_cast<glm::vec3>(light.Position), 1)) : glm::vec3(0));
        shader.SetFloat(locations[slot + 1], light.Strength);
        shader.SetFloat(locations[slot + 2], light.Range);
        shader.SetLinearColor(locations[slot + 3], light.LinearColor);
        shader.SetVector3(locations[slot + 4], glm::vec3(view * glm::vec4(static_cast<glm::vec3>(light.Direction), 0)));
        shader.SetFloat(locations[slot + 5], light.InnerCosine);
        shader.SetFloat(locations[slot + 6], light.OuterCosine);
    }
    shader.SetInt(locations.back(), spots.Count);
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
        values[POINT_COUNT_SLOT] = "_PointLightsCount";
        for (i32 i = 0; i < REI_MAX_SPOT_LIGHTS_COUNT; ++i)
        {
            const auto prefix = "_SpotLights[" + std::to_string(i) + "]";
            const auto slot = SPOT_START_SLOT + i * 7;
            values[slot] = prefix + ".Position";
            values[slot + 1] = prefix + ".Strength";
            values[slot + 2] = prefix + ".Range";
            values[slot + 3] = prefix + ".Color";
            values[slot + 4] = prefix + ".Direction";
            values[slot + 5] = prefix + ".InnerCosine";
            values[slot + 6] = prefix + ".OuterCosine";
        }
        values.back() = "_SpotLightsCount";
        return values;
    }();
    LightLocations locations;
    for (u32 i = 0; i < LIGHT_UNIFORM_COUNT; ++i) locations[i] = shader.GetLocation(names[i]);
    return _lightLocations.emplace(revision, locations).first->second;
}
