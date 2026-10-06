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

void rei::render::LightingRenderModule::SetLightValues(const Shader& shader, const math::Bounds& localBounds, const glm::mat4& modelMatrix) const
{
    REI_PROFILE_SCOPE(profiling::markers::LIGHTING_APPLY.Id);
    profiling::UniformPhaseScope phase(profiling::UniformPhase::Lighting);
    const auto& locations = GetLightLocations(shader);
    Shader::UniformBatch uniforms(shader, false);
    shader.SetFloat(locations[0], _ambientStrength);
    shader.SetLinearColor(locations[1], _ambientLinearColor);
    // Fixed-count/custom shaders can use light slots without _PointLightsCount.
    if (std::none_of(locations.begin() + 2, locations.end(), [](const i32 location) { return location >= 0; })) return;
    const auto worldBounds = localBounds.Transform(modelMatrix);
    std::array<const PointLightSnapshot*, REI_MAX_POINT_LIGHTS_COUNT> selected{};
    i32 count = 0;
    u32 tested = 0;
    const auto* snapshots = _pointSnapshot.data();
    const auto snapshotCount = _pointSnapshot.size();
    for (u64 source = 0; source < snapshotCount && count < _pointLightLimit; ++source)
    {
        const auto& light = snapshots[source];
        ++tested;
        if (!worldBounds.IntersectsSphere(static_cast<glm::vec3>(light.Position), light.Range)) continue;
        selected[count++] = &light;
    }
    profiling::Count(profiling::markers::LIGHTING_TESTED.Id, tested);
    profiling::Count(profiling::markers::LIGHTING_OBJECTS.Id);
    profiling::Count(profiling::markers::LIGHTING_SELECTED.Id, count);
    const PointLightSnapshot empty{};
    for (i32 i = 0; i < REI_MAX_POINT_LIGHTS_COUNT; ++i)
    {
        const auto& light = selected[i] ? *selected[i] : empty;
        const auto slot = 2 + i * 4;
        shader.SetVector3(locations[slot], light.Position);
        shader.SetFloat(locations[slot + 1], light.Strength);
        shader.SetFloat(locations[slot + 2], light.Range);
        shader.SetLinearColor(locations[slot + 3], light.LinearColor);
    }
    shader.SetInt(locations.back(), count);
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
    _pointLightLimit = REI_MAX_POINT_LIGHTS_COUNT;
    const auto& camera = _cameraModule->GetCamera();
    if (!camera.IsNull())
    {
        const auto& settingsRef = camera.Get().GetRendererSettings();
        if (settingsRef.IsLoaded()) _pointLightLimit = settingsRef->GetMaxPointLights();
    }
    _ambientStrength = 0;
    _ambientLinearColor = Color(0, 0, 0, 1);
    if (!_ambientLight.IsNull())
    {
        const auto& ambient = _ambientLight.Get();
        _ambientStrength = ambient.GetStrength();
        _ambientLinearColor = ambient.GetColor().ToLinear();
    }
    _pointSnapshot.clear();
    _pointSnapshot.reserve(_pointLights.size());
    // Keep all enabled sources; select each object's budget of intersecting lights in scene order.
    for (const auto& reference : _pointLights)
    {
        if (reference.IsNull()) continue;
        const auto& light = reference.Get();
        const auto position = light.GetTransform().GetWorldPosition();
        _pointSnapshot.push_back({position, light.GetStrength(), light.GetRange(), light.GetColor().ToLinear()});
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
