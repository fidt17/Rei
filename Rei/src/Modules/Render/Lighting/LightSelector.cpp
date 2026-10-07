#include "pch.h"
#include "LightSelector.h"
#include "Common/Profiling/ProfileMarkers.h"
#include <cstring>

void rei::render::LightSelector::BeginFrame()
{
    ++_frame;
    std::erase_if(_selectionCache, [this](const auto& entry) { return entry.second.LastFrame + 1 < _frame; });
}

const rei::render::LightSelector::Selection& rei::render::LightSelector::Select(const math::Bounds& localBounds, const glm::mat4& modelMatrix, const ecs::Entity object, const LightSnapshot& snapshot) const
{
    auto& selected = object == ecs::NULL_ENTITY ? _uncachedSelection : _selectionCache[object];
    const bool sameBounds = selected.LocalBounds.IsValid() == localBounds.IsValid()
        && (!localBounds.IsValid() || (selected.LocalBounds.Min == localBounds.Min && selected.LocalBounds.Max == localBounds.Max));
    const bool unchanged = !(object == ecs::NULL_ENTITY) && selected.Limit == snapshot.GetPointLightLimit() && selected.Revision == snapshot.GetSelectionRevision() && sameBounds
        && std::memcmp(&selected.ModelMatrix[0].x, &modelMatrix[0].x, sizeof(glm::mat4)) == 0;
    selected.LastFrame = _frame;
    if (unchanged) return selected;

    selected.LocalBounds = localBounds;
    selected.ModelMatrix = modelMatrix;
    selected.Revision = snapshot.GetSelectionRevision();
    selected.Limit = snapshot.GetPointLightLimit();
    selected.Count = 0;
    const auto worldBounds = localBounds.Transform(modelMatrix);
    u32 tested = 0;
    for (u64 source = 0; source < snapshot.GetPointLights().size() && selected.Count < snapshot.GetPointLightLimit(); ++source)
    {
        const auto& light = snapshot.GetPointLights()[source];
        ++tested;
        if (!worldBounds.IntersectsSphere(static_cast<glm::vec3>(light.Position), light.Range)) continue;
        selected.Indices[selected.Count++] = source;
    }
    profiling::Count(profiling::markers::LIGHTING_TESTED.Id, tested);
    return selected;
}
