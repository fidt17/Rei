#include "pch.h"
#include "LightSelector.h"
#include "LightUtility.h"
#include "Common/Profiling/ProfileMarkers.h"
#include <cstring>

namespace
{
    template <typename TLight, typename TIntersects>
    void SelectLights(rei::render::LightSelector::Selection& selected, const rei::math::Bounds& localBounds, const glm::mat4& modelMatrix, const rei::ecs::Entity object, const u64 frame, const i32 limit, const u64 revision, const std::vector<TLight>& lights, TIntersects intersects)
    {
        const bool sameBounds = selected.LocalBounds.IsValid() == localBounds.IsValid()
            && (!localBounds.IsValid() || (selected.LocalBounds.Min == localBounds.Min && selected.LocalBounds.Max == localBounds.Max));
        const bool unchanged = !(object == rei::ecs::NULL_ENTITY) && selected.Limit == limit && selected.Revision == revision && sameBounds
            && std::memcmp(&selected.ModelMatrix[0].x, &modelMatrix[0].x, sizeof(glm::mat4)) == 0;
        selected.LastFrame = frame;
        if (unchanged) return;
        selected.LocalBounds = localBounds;
        selected.ModelMatrix = modelMatrix;
        selected.Revision = revision;
        selected.Limit = limit;
        selected.Count = 0;
        const auto worldBounds = localBounds.Transform(modelMatrix);
        u32 tested = 0;
        for (u64 source = 0; source < lights.size() && selected.Count < limit; ++source)
        {
            ++tested;
            if (!intersects(worldBounds, lights[source])) continue;
            selected.Indices[selected.Count++] = source;
        }
        rei::profiling::Count(rei::profiling::markers::LIGHTING_TESTED.Id, tested);
    }
}

void rei::render::LightSelector::BeginFrame()
{
    ++_frame;
    const auto expired = [this](const auto& entry) { return entry.second.LastFrame + 1 < _frame; };
    std::erase_if(_selectionCache, expired);
    std::erase_if(_spotSelectionCache, expired);
}

const rei::render::LightSelector::Selection& rei::render::LightSelector::Select(const math::Bounds& localBounds, const glm::mat4& modelMatrix, const ecs::Entity object, const LightSnapshot& snapshot) const
{
    auto& selected = object == ecs::NULL_ENTITY ? _uncachedSelection : _selectionCache[object];
    SelectLights(selected, localBounds, modelMatrix, object, _frame, snapshot.GetPointLightLimit(), snapshot.GetSelectionRevision(), snapshot.GetPointLights(),
        [](const auto& bounds, const auto& light) { return bounds.IntersectsSphere(static_cast<glm::vec3>(light.Position), light.Range); });
    return selected;
}

const rei::render::LightSelector::Selection& rei::render::LightSelector::SelectSpots(const math::Bounds& localBounds, const glm::mat4& modelMatrix, const ecs::Entity object, const LightSnapshot& snapshot) const
{
    auto& selected = object == ecs::NULL_ENTITY ? _uncachedSelection : _spotSelectionCache[object];
    SelectLights(selected, localBounds, modelMatrix, object, _frame, snapshot.GetSpotLightLimit(), snapshot.GetSpotSelectionRevision(), snapshot.GetSpotLights(),
        [](const auto& bounds, const auto& light) { return light_utility::IntersectsSpot(bounds, light.Position, light.Direction, light.Range, light.OuterCosine); });
    return selected;
}
