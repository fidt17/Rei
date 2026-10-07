#include "pch.h"
#include "LightSnapshot.h"
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

void rei::render::LightSnapshot::Update(const ecs::ComponentRef<Camera>& camera)
{
    FindAmbientLights();
    FindPointLights();
    BuildSnapshot(camera);
}

void rei::render::LightSnapshot::FindAmbientLights()
{
    _ambientLight = {};
    VisitEnabledLights<AmbientLight>([this](const auto& light)
    {
        _ambientLight = light;
        return false;
    });
}

void rei::render::LightSnapshot::FindPointLights()
{
    _pointLights.clear();
    VisitEnabledLights<PointLight>([this](const auto& light)
    {
        _pointLights.emplace_back(light);
        return true;
    });
}

void rei::render::LightSnapshot::BuildSnapshot(const ecs::ComponentRef<Camera>& camera)
{
    _pointLightLimit = RendererSettings{}.GetMaxPointLights();
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
    bool changed = _pointSnapshot.size() != _pointLights.size();
    u64 index = 0;
    _pointSnapshot.reserve(_pointLights.size());
    // Compare selection inputs once per frame. Strength/color do not affect intersection.
    for (const auto& reference : _pointLights)
    {
        if (reference.IsNull()) continue;
        const auto& light = reference.Get();
        const PointLightData next{light.GetEntity(), light.GetTransform().GetWorldPosition(), light.GetStrength(), light.GetRange(), light.GetColor().ToLinear()};
        if (index < _pointSnapshot.size())
        {
            const auto& previous = _pointSnapshot[index];
            changed |= !(previous.Entity == next.Entity) || previous.Position.x != next.Position.x || previous.Position.y != next.Position.y
                || previous.Position.z != next.Position.z || previous.Range != next.Range;
            _pointSnapshot[index] = next;
        }
        else
        {
            changed = true;
            _pointSnapshot.push_back(next);
        }
        ++index;
    }
    changed |= _pointSnapshot.size() != index;
    _pointSnapshot.resize(index);
    if (changed) ++_selectionRevision;
}
